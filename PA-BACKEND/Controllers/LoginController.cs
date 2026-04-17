using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PA_BACKEND.Data;
using PA_BACKEND.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PA_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly UsuarioService _service;
        private readonly IConfiguration _config;

        public LoginController(
            UsuarioService service,
            IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            try
            {
                var user = await _service.Login(dto.Email, dto.Password);

                if (user == null)
                    return Unauthorized("Credenciales incorrectas");

                var token = GenerateToken(user);

                return Ok(new
                {
                    message = "Login exitoso",
                    token = token,
                    user = user
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message); // temporal
            }
        }

        private string GenerateToken(UsuarioDTO user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.FirstName),
                new Claim("LastName", user.LastName),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim("UserId", user.Id.ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"])
            );

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: "PetsAPI",
                audience: "PetsAPI",
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}