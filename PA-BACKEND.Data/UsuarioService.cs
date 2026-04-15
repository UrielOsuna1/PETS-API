using Npgsql;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using PA_BACKEND.DTOs;

namespace PA_BACKEND.Data
{
    public class UsuarioService
    {
        private readonly PostgreSQLConfiguration _config;

        public UsuarioService(PostgreSQLConfiguration config)
        {
            _config = config;
        }

        // 🔐 HASH
        private string Hash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

        // 🔐 ENCRYPT (bytea)
        private byte[] Encrypt(string input)
        {
            return Encoding.UTF8.GetBytes(input);
        }

        // 🔹 REGISTRAR
        public async Task RegistrarUsuario(UsuarioDTO dto)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var emailHash = Hash(dto.Email);
            var emailEncrypted = Encrypt(dto.Email);

            var phoneHash = string.IsNullOrEmpty(dto.Phone) ? null : Hash(dto.Phone);
            var phoneEncrypted = string.IsNullOrEmpty(dto.Phone) ? null : Encrypt(dto.Phone);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var cmd = new NpgsqlCommand(@"
                INSERT INTO users
                (first_name, last_name, email_encrypted, email_hash, phone_encrypted, phone_hash, password_hash, role_id, is_active)
                VALUES
                (@fn, @ln, @ee, @eh, @pe, @ph, @pass, 2, true)", conn);

            cmd.Parameters.AddWithValue("@fn", dto.FirstName);
            cmd.Parameters.AddWithValue("@ln", dto.LastName);
            cmd.Parameters.AddWithValue("@ee", emailEncrypted);
            cmd.Parameters.AddWithValue("@eh", emailHash);
            cmd.Parameters.AddWithValue("@pe", (object?)phoneEncrypted ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ph", (object?)phoneHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pass", passwordHash);

            await cmd.ExecuteNonQueryAsync();
        }

        // 🔹 LISTAR
        public async Task<List<UsuarioDTO>> ObtenerUsuarios()
        {
            var lista = new List<UsuarioDTO>();

            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT id, first_name, last_name
                FROM users
                WHERE deleted_at IS NULL", conn);

            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                lista.Add(new UsuarioDTO
                {
                    Id = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2)
                });
            }

            return lista;
        }

        // 🔹 EDITAR
        public async Task EditarUsuario(int id, UsuarioDTO dto)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                UPDATE users 
                SET first_name = @fn,
                    last_name = @ln,
                    updated_at = NOW()
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@fn", dto.FirstName);
            cmd.Parameters.AddWithValue("@ln", dto.LastName);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }

        // 🔹 ELIMINAR (SOFT DELETE)
        public async Task EliminarUsuario(int id)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                UPDATE users 
                SET deleted_at = NOW()
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}