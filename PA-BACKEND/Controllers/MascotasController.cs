using Microsoft.AspNetCore.Mvc;
using PA_BACKEND.Data;
using PA_BACKEND.Model;

namespace PA_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MascotasController : ControllerBase
    {
        private readonly MascotaService _service;

        public MascotasController(MascotaService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var data = await _service.GetMascotas();
            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Post(Mascota mascota)
        {
            await _service.CrearMascota(mascota);
            return Ok();
        }

        [HttpPut("{id}")] 
        public async Task<IActionResult> Put(int id, [FromBody] Mascota mascota)
        {
       
            mascota.Id = id;

            await _service.ActualizarMascota(mascota);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.EliminarMascota(id);
            return Ok();
        }
    }
}