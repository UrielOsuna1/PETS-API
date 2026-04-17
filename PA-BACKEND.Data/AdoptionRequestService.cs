using Npgsql;
using PA_BACKEND.DTOs;

namespace PA_BACKEND.Data
{
    public class AdoptionRequestService
    {
        private readonly PostgreSQLConfiguration _config;

        public AdoptionRequestService(PostgreSQLConfiguration config)
        {
            _config = config;
        }

        public async Task Crear(AdoptionRequestCreateDTO dto)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                INSERT INTO adoption_requests
                (user_id, pet_id, status_id, message, created_at)
                VALUES
                (@uid, @pid, 1, @msg, NOW())", conn);

            cmd.Parameters.AddWithValue("@uid", dto.UserId);
            cmd.Parameters.AddWithValue("@pid", dto.PetId);
            cmd.Parameters.AddWithValue("@msg", dto.Message);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<AdoptionRequestDTO>> ObtenerTodas()
        {
            var lista = new List<AdoptionRequestDTO>();

            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT id, user_id, pet_id, status_id, message, created_at
                FROM adoption_requests
                WHERE deleted_at IS NULL
                ORDER BY created_at DESC", conn);

            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                lista.Add(new AdoptionRequestDTO
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    PetId = reader.GetInt32(2),
                    StatusId = reader.GetInt32(3),
                    Message = reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }

            return lista;
        }

        public async Task CambiarEstado(int id, int statusId, int adminId)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                UPDATE adoption_requests
                SET status_id = @sid,
                    reviewed_by = @aid,
                    reviewed_at = NOW(),
                    updated_at = NOW()
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@sid", statusId);
            cmd.Parameters.AddWithValue("@aid", adminId);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}