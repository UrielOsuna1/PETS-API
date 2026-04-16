using Microsoft.AspNetCore.Mvc;
using PA_BACKEND.Data;
using PA_BACKEND.DTOs;

namespace PA_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly UsuarioService _service;

        public LoginController(UsuarioService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            try
            {
                var user = await _service.Login(dto.Email, dto.Password);

                if (user == null)
                    return Unauthorized("Credenciales incorrectas");

                return Ok(new
                {
                    message = "Login exitoso",
                    user
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}