using Npgsql;
using NpgsqlTypes;
using BCrypt.Net;
using Dapper;
using Microsoft.Extensions.Configuration;
using System.Data;
// dtos
using PA_BACKEND.DTOs.Auth;
using PA_BACKEND.DTOs.Common;
// interfaces
using PA_BACKEND.Data.Interface;

namespace PA_BACKEND.Data.Repositories
{
    /// <summary>
    /// implementación del repositorio de autenticación.
    /// contiene la lógica de autenticación y generación de tokens.
    /// </summary>
    public class AuthRepository : IAuthRepository
    {
        private readonly NpgsqlConnection _connection;
        private readonly PostgreSQLConfiguration _configuration;
        private readonly IConfiguration _appConfiguration;
        private readonly ITokenRepository _tokenRepository;
        private readonly bool _isDevelopment;

        public AuthRepository(PostgreSQLConfiguration configuration, IConfiguration appConfiguration, ITokenRepository tokenRepository)
        {
            _configuration = configuration;
            _appConfiguration = appConfiguration;
            _tokenRepository = tokenRepository;
            _connection = new NpgsqlConnection(configuration.GetConnection().ConnectionString);
            _isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        protected NpgsqlConnection GetConnection()
            => new NpgsqlConnection(_configuration.GetConnection().ConnectionString);

        /// <summary>
        /// normaliza el correo electrónico (trim y minúsculas).
        /// flujo: elimina espacios -> convierte a minúsculas -> retorna string limpio
        /// </summary>
        /// <param name="email">correo electrónico a normalizar</param>
        /// <returns>correo normalizado en minúsculas sin espacios</returns>
        #region normalizar email
        private string NormalizeEmail(string email)
            => email?.Trim().ToLowerInvariant() ?? string.Empty;
        #endregion

        /// <summary>
        /// registra un nuevo usuario (adoptante) en la base de datos.
        /// flujo: valida datos -> hashea contraseña -> crea usuario -> genera tokens -> retorna respuesta
        /// </summary>
        /// <param name="registerUserDTO">datos del nuevo usuario</param>
        /// <returns>tokens de acceso y refresh del usuario registrado</returns>
        #region registrar usuario
        public async Task<ResponseLoginDTO> RegisterUserAsync(RegisterUserDTO registerUserDTO)
        {
            if (registerUserDTO == null)
                throw new ArgumentNullException(nameof(registerUserDTO));

            if (string.IsNullOrWhiteSpace(registerUserDTO.Email) ||
                string.IsNullOrWhiteSpace(registerUserDTO.Password) ||
                string.IsNullOrWhiteSpace(registerUserDTO.FirstName) ||
                string.IsNullOrWhiteSpace(registerUserDTO.LastName))
                throw new ArgumentException("Campos requeridos");

            try
            {
                var normalizedEmail = NormalizeEmail(registerUserDTO.Email);
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(registerUserDTO.Password);
                string refreshToken = _tokenRepository.GenerateRefreshToken();

                using var connection = GetConnection();
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    (int UserId, string EmailHash) dbResult;

                    try
                    {                        
                        dbResult = await connection.QueryFirstOrDefaultAsync<(int UserId, string EmailHash)>(
                            "select * from public.fun_create_user(@p_first_name::varchar, @p_last_name::varchar, @p_email::varchar, @p_password_hash::varchar, @p_role_id::integer, @p_phone::varchar)",
                        new {
                            p_first_name = registerUserDTO.FirstName.Trim(),
                            p_last_name = registerUserDTO.LastName.Trim(),
                            p_email = normalizedEmail,
                            p_password_hash = hashedPassword,
                            p_role_id = 2,
                            p_phone = registerUserDTO.Phone?.Trim()
                        },
                        transaction
                    );
                    }
                    catch (PostgresException ex) when (ex.MessageText.Contains("Ya existe un usuario con ese email"))
                    {
                        // captura el error de duplicado que lanza la función SQL
                        throw new InvalidOperationException(SecureMessages.UserAlreadyExists);
                    }

                    if (dbResult.UserId <= 0)
                        throw new InvalidOperationException("No se pudo registrar el usuario.");

                    await StoreRefreshTokenAsync(dbResult.UserId, refreshToken, connection, transaction);

                    string accessToken = _tokenRepository.GenerateAccessToken(
                        dbResult.UserId,
                        "ADOPTANTE",
                        _tokenRepository.ExtractTokenId(refreshToken)
                    );

                    await transaction.CommitAsync();

                    return new ResponseLoginDTO {
                        AccessToken  = accessToken,
                        RefreshToken = refreshToken
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(SecureMessages.InternalServerError);
            }
        }
        #endregion

        /// <summary>
        /// login de usuario.
        /// flujo: valida credenciales -> verifica usuario -> revoca tokens antiguos -> genera nuevos tokens
        /// </summary>
        /// <param name="requestLoginDTO">credenciales de login</param>
        /// <returns>tokens de acceso y refresh del usuario autenticado</returns>
        #region login
        public async Task<ResponseLoginDTO> LoginUserAsync(RequestLoginDTO requestLoginDTO)
        {
            if (requestLoginDTO == null)
                throw new ArgumentNullException(nameof(requestLoginDTO));

            if (string.IsNullOrWhiteSpace(requestLoginDTO.Email) ||
                string.IsNullOrWhiteSpace(requestLoginDTO.Password))
                throw new ArgumentException("Email y contraseña requeridos");

            try
            {
                var normalizedEmail = NormalizeEmail(requestLoginDTO.Email);

                using var connection = GetConnection();
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "select * from public.fun_get_user_for_login(@Email::varchar)",
                        new { Email = normalizedEmail },
                        transaction
                    );

                    // usuario no encontrado o contraseña incorrecta 
                    if (result == null)
                        throw new InvalidOperationException(SecureMessages.InvalidCredentials);

                    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(requestLoginDTO.Password, (string)result.password_hash);
                    if (!isPasswordValid)
                        throw new InvalidOperationException(SecureMessages.InvalidCredentials);

                    // verifica que el usuario esté activo después de validar credenciales
                    if (!(bool)result.is_active)
                        throw new InvalidOperationException(SecureMessages.InvalidCredentials);

                    await RevokeAllUserRefreshTokensAsync((int)result.user_id, connection, transaction);

                    string refreshToken = _tokenRepository.GenerateRefreshToken();
                    await StoreRefreshTokenAsync((int)result.user_id, refreshToken, connection, transaction);

                    string accessToken = _tokenRepository.GenerateAccessToken(
                        (int)result.user_id,
                        (string)result.role_name,
                        _tokenRepository.ExtractTokenId(refreshToken)
                    );

                    await transaction.CommitAsync();

                    return new ResponseLoginDTO {
                        AccessToken  = accessToken,
                        RefreshToken = refreshToken
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(SecureMessages.InternalServerError);
            }
        }
        #endregion

        /// <summary>
        /// almacena el refresh token en la base de datos.
        /// flujo: valida parámetros -> inserta token con expiración -> retorna sin error
        /// </summary>
        /// <param name="userId">id del usuario</param>
        /// <param name="refreshToken">token a almacenar</param>
        /// <param name="connection">conexión a base de datos</param>
        /// <param name="transaction">transacción opcional</param>
        #region almacenar refresh token 
        private async Task StoreRefreshTokenAsync(int userId, string refreshToken, NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Parámetros inválidos");

            var tokenId    = _tokenRepository.ExtractTokenId(refreshToken);
            var randomValue = _tokenRepository.ExtractRandomValue(refreshToken);

            if (!Guid.TryParse(tokenId, out var tokenIdGuid) || string.IsNullOrWhiteSpace(randomValue))
                throw new ArgumentException("Formato de token inválido");

            string hashedRandomValue = BCrypt.Net.BCrypt.HashPassword(randomValue);

            await connection.ExecuteAsync(
                "select * from public.fun_insert_refresh_token(@p_user_id, @p_token_id, @p_token_hash, @p_expires_at)",
                new {
                    p_user_id    = userId,
                    p_token_id   = tokenIdGuid,
                    p_token_hash = hashedRandomValue,
                    p_expires_at = DateTime.UtcNow.AddDays(
                        double.TryParse(
                            _appConfiguration["RefreshToken:ExpirationDays"], 
                            out double days
                        ) && days > 0 ? days : 1
                    )
                },
                transaction
            );
        }
        #endregion

        /// <summary>
        /// renueva los tokens de acceso usando un refresh token válido.
        /// flujo: valida refresh token -> revoca token anterior -> genera nuevo par de tokens -> retorna respuesta
        /// </summary>
        /// <param name="refreshToken">refresh token a renovar</param>
        /// <returns>nuevo par de tokens (access y refresh)</returns>
        #region renovar tokens
        public async Task<ResponseLoginDTO> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token requerido");

            var tokenId = _tokenRepository.ExtractTokenId(refreshToken);
            var randomValue = _tokenRepository.ExtractRandomValue(refreshToken);

            if (!Guid.TryParse(tokenId, out var tokenIdGuid) || string.IsNullOrWhiteSpace(randomValue))
                throw new ArgumentException("Formato de token inválido");

            using var connection = GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var tokenRecord = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "select * from public.fun_get_refresh_token_by_token_id(@p_token_id)",
                    new { p_token_id = tokenIdGuid },
                    transaction
                );

                if (tokenRecord == null)
                    throw new InvalidOperationException(SecureMessages.InvalidToken);

                // valida revocación antes de validar el hash
                if ((bool)tokenRecord.is_revoked)
                    throw new InvalidOperationException(SecureMessages.InvalidToken);

                // valida expiración
                if (DateTime.UtcNow > Convert.ToDateTime(tokenRecord.expires_at).ToUniversalTime())
                    throw new InvalidOperationException(SecureMessages.TokenExpired);

                // valida hash
                bool isValidHash = BCrypt.Net.BCrypt.Verify(randomValue, (string)tokenRecord.token_hash);
                if (!isValidHash)
                    throw new InvalidOperationException(SecureMessages.InvalidToken);

                // revoca el token usado
                await connection.ExecuteAsync(
                    "select * from public.fun_revoke_refresh_token(@p_token_id)",
                    new { p_token_id = tokenIdGuid },
                    transaction
                );

                // enforce límite de dispositivos
                await RevokeAllUserRefreshTokensAsync((int)tokenRecord.user_id, connection, transaction);

                string newRefreshToken = _tokenRepository.GenerateRefreshToken();
                await StoreRefreshTokenAsync((int)tokenRecord.user_id, newRefreshToken, connection, transaction);

                string newAccessToken = _tokenRepository.GenerateAccessToken(
                    (int)tokenRecord.user_id,
                    (string)tokenRecord.role_name,
                    _tokenRepository.ExtractTokenId(newRefreshToken)
                );

                await transaction.CommitAsync();

                return new ResponseLoginDTO {
                    AccessToken  = newAccessToken,
                    RefreshToken = newRefreshToken
                };
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(SecureMessages.InternalServerError);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        #endregion

        /// <summary>
        /// cierra la sesión de un usuario específico.
        /// flujo: valida parámetros -> extrae jti del access token -> marca refresh token como revocado -> confirma logout
        /// </summary>
        /// <param name="userId">id del usuario</param>
        /// <param name="accessToken">token de acceso actual</param>
        #region logout
        public async Task LogoutAsync(int userId, string accessToken)
        {
            if (userId <= 0)
                throw new ArgumentException("ID de usuario inválido");

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token requerido");

            try
            {
                // extrae jti y expiración del jwt
                var jti       = _tokenRepository.ExtractJti(accessToken);
                var expiresAt = _tokenRepository.ExtractExpiration(accessToken);

                if (!Guid.TryParse(jti, out var jtiGuid))
                    throw new ArgumentException("Token inválido");

                using var connection = GetConnection();
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    var parameters = new DynamicParameters();
                    parameters.Add("p_user_id", userId, DbType.Int32);
                    parameters.Add("p_jti", jtiGuid, DbType.Guid);
                    parameters.Add("p_expires_at", expiresAt.ToUniversalTime(), DbType.DateTime);

                    await connection.ExecuteAsync(
                        "select public.fun_logout(@p_user_id, @p_jti, @p_expires_at)",
                        parameters,
                        transaction
                    );

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(SecureMessages.InternalServerError);
            }
        }
        #endregion

        /// <summary>
        /// revoca tokens excedentes — conserva los p_max_devices más recientes.
        /// flujo: ejecuta función de base de datos -> mantiene tokens más recientes -> revoca antiguos
        /// </summary>
        /// <param name="userId">id del usuario</param>
        /// <param name="connection">conexión a base de datos</param>
        /// <param name="transaction">transacción actual</param>
        #region gestionar refresh tokens
        private async Task RevokeAllUserRefreshTokensAsync(int userId, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            if (userId <= 0)
                return;

            var revokedCount = await connection.QueryFirstOrDefaultAsync<int>(
                "select * from public.fun_enforce_refresh_token_limit(@p_user_id, @p_max_devices)",
                new {
                    p_user_id = userId,
                    p_max_devices = 2
                },
                transaction
            );

            if (revokedCount > 0); // tokens revocados
        }
        #endregion

        /// <summary>
        /// revoca todas las sesiones de un usuario (logout global).
        /// flujo: valida id -> conecta a base de datos -> revoca todos los refresh tokens -> confirma operación
        /// </summary>
        /// <param name="userId">id del usuario</param>
        #region logout global
        public async Task RevokeAllUserSessionsAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("ID de usuario inválido");

            try
            {
                using var connection = GetConnection();
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    var revokedCount = await connection.QueryFirstOrDefaultAsync<int>(
                        "select * from public.fun_enforce_refresh_token_limit(@p_user_id, @p_max_devices)",
                        new {
                            p_user_id = userId,
                            p_max_devices = 0 // 0 = revocar todos
                        },
                        transaction
                    );
                    await transaction.CommitAsync();

                    if (revokedCount > 0); // sesiones revocadas
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException(SecureMessages.InternalServerError);
            }
        }
        #endregion

        /// <summary>
        /// obtiene información de sesión del usuario.
        /// flujo: valida id -> conecta a base de datos -> ejecuta función -> mapea resultado
        /// </summary>
        /// <param name="userId">id del usuario</param>
        /// <returns>información de sesión del usuario</returns>
        #region obtener información de sesión
        public async Task<SessionInformationDTO> GetSessionInformationAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("ID de usuario inválido");

            try
            {
                using var connection = GetConnection();
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("p_user_id", userId, DbType.Int32);

                var result = await connection.QueryFirstOrDefaultAsync<SessionInformationDTO>(
                    "SELECT first_name AS FirstName, last_name AS LastName, email AS Email, phone AS Phone, created_at AS CreatedAt FROM public.fun_obtener_informacion_sesion_usuario(@p_user_id)",
                    parameters
                );

                if (result == null)
                    throw new InvalidOperationException("Usuario no encontrado");

                return result;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(SecureMessages.InternalServerError);
            }
        }
        #endregion
    }
}