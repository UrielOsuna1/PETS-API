using Microsoft.AspNetCore.Mvc;
using PA_BACKEND.Data;
using PA_BACKEND.DTOs;

namespace PA_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdoptionRequestsController : ControllerBase
    {
        private readonly AdoptionRequestService _service;

        public AdoptionRequestsController(
            AdoptionRequestService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] AdoptionRequestCreateDTO dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("DTO vacío");

                Console.WriteLine($"USER: {dto.UserId}");
                Console.WriteLine($"PET: {dto.PetId}");
                Console.WriteLine($"MSG: {dto.Message}");

                if (dto.UserId <= 0)
                    return BadRequest("UserId inválido");

                if (dto.PetId <= 0)
                    return BadRequest("PetId inválido");

                if (string.IsNullOrWhiteSpace(dto.Message))
                    return BadRequest("Mensaje requerido");

                await _service.Crear(dto);

                return Ok(new { message = "Solicitud enviada" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("🔥 ERROR REAL:");
                Console.WriteLine(ex.ToString());

                return StatusCode(500, ex.ToString());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodas()
        {
            var data = await _service.ObtenerTodas();
            return Ok(data);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Aprobar(int id)
        {
            await _service.CambiarEstado(id, 2, 1);
            return Ok("Solicitud aprobada");
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> Rechazar(int id)
        {
            await _service.CambiarEstado(id, 3, 1);
            return Ok("Solicitud rechazada");
        }
    }
}