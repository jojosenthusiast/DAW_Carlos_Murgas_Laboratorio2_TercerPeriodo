using System.Security.Claims;

namespace AutoFix.Mvc.Infraestructura;

public static class Claves
{
    public const string Token = "access_token";
    public const string Permiso = "permiso";
    public const string ClienteId = "clienteId";
    public const string MecanicoId = "mecanicoId";
}

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";
    public const string Mecanico = "Mecanico";
    public const string Supervisor = "Supervisor";
    public const string Cliente = "Cliente";

    public const string Personal = "Administrador,Recepcionista,Mecanico,Supervisor";
    public const string Gestion = "Administrador,Recepcionista";
    public const string Mandos = "Administrador,Supervisor";
}

public static class Permisos
{
    public const string OrdenesCrear = "Ordenes.Crear";
    public const string OrdenesEditar = "Ordenes.Editar";
    public const string OrdenesAsignar = "Ordenes.Asignar";
    public const string OrdenesAprobar = "Ordenes.Aprobar";
    public const string OrdenesFinalizar = "Ordenes.Finalizar";
    public const string InventarioGestionar = "Inventario.Gestionar";
    public const string ReportesConsultar = "Reportes.Consultar";
}

/// <summary>Atajos para mostrar u ocultar botones. Ojo: ocultar NO es seguridad; la API valida todo.</summary>
public static class UsuarioExtensions
{
    public static bool Puede(this ClaimsPrincipal u, string permiso) => u.HasClaim(Claves.Permiso, permiso);
    public static bool EsPersonal(this ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && !u.IsInRole(Roles.Cliente);
    public static string Rol(this ClaimsPrincipal u) => u.FindFirst(ClaimTypes.Role)?.Value ?? "";
    public static int? MecanicoId(this ClaimsPrincipal u) => int.TryParse(u.FindFirst(Claves.MecanicoId)?.Value, out var v) ? v : null;
    public static int? ClienteId(this ClaimsPrincipal u) => int.TryParse(u.FindFirst(Claves.ClienteId)?.Value, out var v) ? v : null;
}
