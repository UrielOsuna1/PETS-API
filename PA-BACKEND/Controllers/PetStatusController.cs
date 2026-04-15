using Microsoft.AspNetCore.Mvc;
using PA_BACKEND.Data;

namespace PA_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PetStatusController : ControllerBase
    {
        private readonly PetStatusService _service;

        public PetStatusController(PetStatusService service)
        {
            _service = service;
        }

        // 🔹 LISTAR
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var lista = await _service.ObtenerPetStatus();
            return Ok(lista);
        }
    }
}