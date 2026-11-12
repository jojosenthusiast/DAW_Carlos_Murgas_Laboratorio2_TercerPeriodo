using System.Security.Claims;
using System.Text;
using AutoFix.Api.Common;
using AutoFix.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AutoFix.Api.Services;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "AutoFix.Api";
    public string Audience { get; set; } = "AutoFix.Clientes";
    public int ExpiracionMinutos { get; set; } = 120;
}

public class TokenService
{
    private readonly JwtOptions _opciones;
    public TokenService(IOptions<JwtOptions> opciones) => _opciones = opciones.Value;

    public (string Token, DateTime ExpiraUtc) Generar(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new("sub", usuario.Id.ToString()),
            new("email", usuario.Email),
            new("name", usuario.NombreCompleto),
            new("role", usuario.Rol),
            new("jti", Guid.NewGuid().ToString())
        };
        if (usuario.ClienteId is int clienteId) claims.Add(new Claim("clienteId", clienteId.ToString()));
        if (usuario.MecanicoId is int mecanicoId) claims.Add(new Claim("mecanicoId", mecanicoId.ToString()));
        foreach (var permiso in usuario.Permisos.Select(p => p.Permiso).Distinct())
            claims.Add(new Claim(Permisos.Claim, permiso));

        var expira = DateTime.UtcNow.AddMinutes(_opciones.ExpiracionMinutos);
        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expira,
            IssuedAt = DateTime.UtcNow,
            Issuer = _opciones.Issuer,
            Audience = _opciones.Audience,
            SigningCredentials = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256)
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
