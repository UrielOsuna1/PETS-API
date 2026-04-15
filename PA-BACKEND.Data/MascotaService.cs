using Npgsql;
using PA_BACKEND.Model;

namespace PA_BACKEND.Data
{
    public class MascotaService
    {
        private readonly PostgreSQLConfiguration _config;

        public MascotaService(PostgreSQLConfiguration config)
        {
            _config = config;
        }

        // 🔹 GET
        public async Task<List<Mascota>> GetMascotas()
        {
            var lista = new List<Mascota>();

            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT * FROM pets", conn);
            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                lista.Add(new Mascota
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Species = reader.GetString(2),
                    Breed = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Age = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Size = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Gender = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                    StatusId = reader.GetInt32(8),
                    Img = reader.IsDBNull(reader.GetOrdinal("img")) ? null : reader.GetString(reader.GetOrdinal("img"))
                });
            }

            return lista;
        }

        // 🔹 POST (CREAR)
        public async Task CrearMascota(Mascota mascota)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                INSERT INTO pets 
                (name, species, breed, age, size, gender, description, status_id, img) 
                VALUES (@name, @species, @breed, @age, @size, @gender, @desc, @status, @img)", conn);

            cmd.Parameters.AddWithValue("@name", mascota.Name);
            cmd.Parameters.AddWithValue("@species", mascota.Species);
            cmd.Parameters.AddWithValue("@breed", mascota.Breed ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@age", mascota.Age ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@size", mascota.Size ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@gender", mascota.Gender ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@desc", mascota.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", mascota.StatusId);
            cmd.Parameters.AddWithValue("@img", mascota.Img ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        // 🔹 PUT (ACTUALIZAR)
        public async Task ActualizarMascota(Mascota mascota)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                UPDATE pets SET
                    name = @name,
                    species = @species,
                    breed = @breed,
                    age = @age,
                    size = @size,
                    gender = @gender,
                    description = @desc,
                    status_id = @status,
                    img = @img,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id", mascota.Id);
            cmd.Parameters.AddWithValue("@name", mascota.Name);
            cmd.Parameters.AddWithValue("@species", mascota.Species);
            cmd.Parameters.AddWithValue("@breed", mascota.Breed ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@age", mascota.Age ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@size", mascota.Size ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@gender", mascota.Gender ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@desc", mascota.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", mascota.StatusId);
            cmd.Parameters.AddWithValue("@img", mascota.Img ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        // 🔹 DELETE
        public async Task EliminarMascota(int id)
        {
            using var conn = _config.GetConnection();
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("DELETE FROM pets WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}