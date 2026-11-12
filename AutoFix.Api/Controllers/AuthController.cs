using AutoFix.Api.DTOs;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoFix.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    /// <summary>Inicio de sesión: devuelve el JWT con rol y claims.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginRespuestaDto>> Login(LoginDto dto) => Ok(await _auth.LoginAsync(dto));

    /// <summary>Registro público de clientes (rol Cliente).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("registro")]
    public async Task<ActionResult<LoginRespuestaDto>> Registro(RegistroDto dto) =>
        StatusCode(StatusCodes.Status201Created, await _auth.RegistrarClienteAsync(dto));

    /// <summary>Datos del usuario dueño del token.</summary>
    [HttpGet("perfil")]
    public async Task<ActionResult<UsuarioDto>> Perfil() => Ok(await _auth.PerfilAsync());
}
