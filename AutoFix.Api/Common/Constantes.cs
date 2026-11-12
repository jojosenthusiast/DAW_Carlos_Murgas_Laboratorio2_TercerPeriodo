using Microsoft.AspNetCore.Authorization;

namespace AutoFix.Api.Common;

/// <summary>Roles del sistema (el "puesto" de la persona).</summary>
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";
    public const string Mecanico = "Mecanico";
    public const string Supervisor = "Supervisor";
    public const string Cliente = "Cliente";

    public static readonly string[] Todos = { Administrador, Recepcionista, Mecanico, Supervisor, Cliente };
}

/// <summary>Claims obligatorios (el "permiso concreto").</summary>
public static class Permisos
{
    public const string Claim = "permiso";

    public const string OrdenesCrear = "Ordenes.Crear";
    public const string OrdenesEditar = "Ordenes.Editar";
    public const string OrdenesAsignar = "Ordenes.Asignar";
    public const string OrdenesAprobar = "Ordenes.Aprobar";
    public const string OrdenesFinalizar = "Ordenes.Finalizar";
    public const string InventarioGestionar = "Inventario.Gestionar";
    public const string ReportesConsultar = "Reportes.Consultar";

    public static readonly string[] Todos =
    {
        OrdenesCrear, OrdenesEditar, OrdenesAsignar, OrdenesAprobar,
        OrdenesFinalizar, InventarioGestionar, ReportesConsultar
    };

    /// <summary>Permisos que recibe cada rol por defecto al crear un usuario.</summary>
    public static IReadOnlyList<string> PorDefecto(string rol) => rol switch
    {
        Roles.Administrador => Todos,
        Roles.Recepcionista => new[] { OrdenesCrear, OrdenesEditar },
        Roles.Mecanico => new[] { OrdenesEditar },
        Roles.Supervisor => new[] { OrdenesEditar, OrdenesAsignar, OrdenesAprobar, OrdenesFinalizar, InventarioGestionar, ReportesConsultar },
        _ => Array.Empty<string>()
    };
}

/// <summary>Políticas de autorización: combinan rol + claim.</summary>
public static class Politicas
{
    public const string PersonalTaller = "PersonalTaller";
    public const string GestionClientes = "GestionClientes";
    public const string PuedeCrearOrden = "PuedeCrearOrden";
    public const string PuedeEditarOrden = "PuedeEditarOrden";
    public const string PuedeAsignar = "PuedeAsignar";
    public const string PuedeAprobar = "PuedeAprobar";
    public const string PuedeFinalizar = "PuedeFinalizar";
    public const string PuedeGestionarInventario = "PuedeGestionarInventario";
    public const string PuedeConsultarReportes = "PuedeConsultarReportes";

    public static void Registrar(AuthorizationOptions o)
    {
        // Todo endpoint sin [AllowAnonymous] exige un JWT válido (sin token = 401).
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        o.AddPolicy(PersonalTaller, p => p.RequireRole(Roles.Administrador, Roles.Recepcionista, Roles.Mecanico, Roles.Supervisor));
        o.AddPolicy(GestionClientes, p => p.RequireRole(Roles.Administrador, Roles.Recepcionista));

        o.AddPolicy(PuedeCrearOrden, p => p.RequireRole(Roles.Recepcionista, Roles.Administrador)
            .RequireClaim(Permisos.Claim, Permisos.OrdenesCrear));
        o.AddPolicy(PuedeEditarOrden, p => p.RequireRole(Roles.Recepcionista, Roles.Mecanico, Roles.Supervisor, Roles.Administrador)
            .RequireClaim(Permisos.Claim, Permisos.OrdenesEditar));
        o.AddPolicy(PuedeAsignar, p => p.RequireRole(Roles.Supervisor, Roles.Administrador)
            .RequireClaim(Permisos.Claim, Permisos.OrdenesAsignar));
        o.AddPolicy(PuedeAprobar, p => p.RequireRole(Roles.Supervisor, Roles.Administrador)
            .RequireClaim(Permisos.Claim, Permisos.OrdenesAprobar));
        o.AddPolicy(PuedeFinalizar, p => p.RequireRole(Roles.Supervisor, Roles.Administrador)
            .RequireClaim(Permisos.Claim, Permisos.OrdenesFinalizar));
        o.AddPolicy(PuedeGestionarInventario, p => p.RequireRole(Roles.Administrador, Roles.Supervisor)
            .RequireClaim(Permisos.Claim, Permisos.InventarioGestionar));
        o.AddPolicy(PuedeConsultarReportes, p => p.RequireRole(Roles.Administrador, Roles.Supervisor, Roles.Recepcionista)
            .RequireClaim(Permisos.Claim, Permisos.ReportesConsultar));
    }
}
