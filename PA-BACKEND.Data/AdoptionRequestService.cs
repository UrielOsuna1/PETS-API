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

        // =====================================================
        // CREAR SOLICITUD
        // =====================================================
        public async Task Crear(AdoptionRequestCreateDTO dto)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            try
            {
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
            catch (Exception ex)
            {
                throw new Exception("Error al insertar en BD: " + ex.Message);
            }
        }

        // =====================================================
        // OBTENER TODAS LAS SOLICITUDES (ADMIN)
        // =====================================================
        public async Task<List<AdoptionRequestDTO>> ObtenerTodas()
        {
            var lista = new List<AdoptionRequestDTO>();

            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
SELECT
    ar.id,
    ar.user_id,
    ar.pet_id,
    ar.status_id,
    ar.message,
    ar.created_at,
    ar.reviewed_at,

    u.first_name,
    u.last_name,

    p.name,
    p.species,
    p.breed,
    p.age,
    p.img

FROM adoption_requests ar

INNER JOIN users u
    ON u.id = ar.user_id

INNER JOIN pets p
    ON p.id = ar.pet_id

WHERE ar.deleted_at IS NULL
ORDER BY ar.created_at DESC
", conn);

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
                    CreatedAt = reader.GetDateTime(5),

                    ReviewedAt = reader.IsDBNull(6)
                        ? (DateTime?)null
                        : reader.GetDateTime(6),

                    User = new RequestUserDTO
                    {
                        FullName =
                            reader.GetString(7) + " " +
                            reader.GetString(8),

                        Email = "", // no se puede mostrar por estar cifrado
                        Phone = ""
                    },

                    Pet = new RequestPetDTO
                    {
                        Id = reader.GetInt32(2),
                        Name = reader.GetString(9),
                        Species = reader.GetString(10),
                        Breed = reader.GetString(11),
                        AgeYears = reader.GetInt32(12),
                        Images = new List<RequestPetImageDTO>
                        {
                            new RequestPetImageDTO
                            {
                                ImageUrl = reader.IsDBNull(13)
                                    ? ""
                                    : reader.GetString(13),

                                IsPrimary = true
                            }
                        }
                    },

                    Status = new RequestStatusDTO
                    {
                        Name = reader.GetInt32(3) switch
                        {
                            1 => "Pendiente",
                            2 => "Aprobada",
                            3 => "Rechazada",
                            _ => "Pendiente"
                        }
                    }
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