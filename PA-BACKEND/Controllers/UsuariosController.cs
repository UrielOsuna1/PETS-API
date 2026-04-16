using Microsoft.AspNetCore.Mvc;
using PA_BACKEND.Data;
using PA_BACKEND.DTOs;

namespace PA_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuarioService _service;

        public UsuariosController(UsuarioService service)
        {
            _service = service;
        }

        // 🔹 REGISTRO
        [HttpPost("registro")]
        public async Task<IActionResult> Registrar(UsuarioDTO dto)
        {
            try
            {
                await _service.RegistrarUsuario(dto);
                return Ok(new { message = "Usuario registrado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno", detail = ex.Message });
            }
        }

        // 🔹 LISTAR
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var usuarios = await _service.ObtenerUsuarios();
            return Ok(usuarios);
        }

        // 🔹 EDITAR
        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, UsuarioDTO dto)
        {
            try
            {
                await _service.EditarUsuario(id, dto);
                return Ok("Usuario actualizado");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 🔹 ELIMINAR
        [HttpDelete("{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            try
            {
                await _service.EliminarUsuario(id);
                return Ok("Usuario eliminado");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}