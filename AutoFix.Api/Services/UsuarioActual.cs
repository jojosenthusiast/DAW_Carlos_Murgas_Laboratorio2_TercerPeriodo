using System.Security.Claims;
using AutoFix.Api.Common;

namespace AutoFix.Api.Services;

/// <summary>Datos del usuario autenticado leídos desde los claims del JWT.</summary>
public interface IUsuarioActual
{
    int? Id { get; }
    string Email { get; }
    string Rol { get; }
    int? ClienteId { get; }
    int? MecanicoId { get; }
    bool EsRol(params string[] roles);
    bool TienePermiso(string permiso);
}

public class UsuarioActual : IUsuarioActual
{
    private readonly IHttpContextAccessor _accessor;
    public UsuarioActual(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public int? Id => Entero("sub");
    public string Email => Principal?.FindFirst("email")?.Value ?? "anónimo";
    public string Rol => Principal?.FindFirst("role")?.Value ?? "";
    public int? ClienteId => Entero("clienteId");
    public int? MecanicoId => Entero("mecanicoId");

    public bool EsRol(params string[] roles) => roles.Contains(Rol);
    public bool TienePermiso(string permiso) => Principal?.HasClaim(Permisos.Claim, permiso) == true;

    private int? Entero(string claim) =>
        int.TryParse(Principal?.FindFirst(claim)?.Value, out var valor) ? valor : null;
}
