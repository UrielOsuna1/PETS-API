using Npgsql;
using PA_BACKEND.DTOs;

namespace PA_BACKEND.Data
{
    public class PetStatusService
    {
        private readonly PostgreSQLConfiguration _config;

        public PetStatusService(PostgreSQLConfiguration config)
        {
            _config = config;
        }

        // 🔹 LISTAR
        public async Task<List<PetStatusDto>> ObtenerPetStatus()
        {
            var lista = new List<PetStatusDto>();

            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT id, name
                FROM pet_status
                ORDER BY id", conn);

            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                lista.Add(new PetStatusDto
                {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1)
                });
            }

            return lista;
        }
    }
}