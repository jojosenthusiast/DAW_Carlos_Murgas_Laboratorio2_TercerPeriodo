using AutoFix.Api.Entities;

namespace AutoFix.Api.Services;

/// <summary>
/// Camino de la orden: Recibido -> En diagnóstico -> Presupuestado -> Pendiente de aprobación
/// -> Aprobado -> En reparación -> Finalizado -> Entregado. Puede cancelarse antes de reparar.
/// </summary>
public static class ReglasEstadoOrden
{
    private static readonly Dictionary<EstadoOrden, EstadoOrden[]> Siguientes = new()
    {
        [EstadoOrden.Recibido] = new[] { EstadoOrden.EnDiagnostico, EstadoOrden.Cancelado },
        [EstadoOrden.EnDiagnostico] = new[] { EstadoOrden.Presupuestado, EstadoOrden.Cancelado },
        [EstadoOrden.Presupuestado] = new[] { EstadoOrden.PendienteAprobacion, EstadoOrden.Cancelado },
        [EstadoOrden.PendienteAprobacion] = new[] { EstadoOrden.Aprobado, EstadoOrden.Presupuestado, EstadoOrden.Cancelado },
        [EstadoOrden.Aprobado] = new[] { EstadoOrden.EnReparacion, EstadoOrden.Cancelado },
        [EstadoOrden.EnReparacion] = new[] { EstadoOrden.Finalizado },
        [EstadoOrden.Finalizado] = new[] { EstadoOrden.Entregado },
        [EstadoOrden.Entregado] = Array.Empty<EstadoOrden>(),
        [EstadoOrden.Cancelado] = Array.Empty<EstadoOrden>()
    };

    /// <summary>Estados que se asignan automáticamente desde el módulo de presupuestos.</summary>
    public static readonly EstadoOrden[] SoloPorPresupuesto =
        { EstadoOrden.Recibido, EstadoOrden.Presupuestado, EstadoOrden.PendienteAprobacion, EstadoOrden.Aprobado };

    public static readonly EstadoOrden[] Manuales =
        { EstadoOrden.EnDiagnostico, EstadoOrden.EnReparacion, EstadoOrden.Finalizado, EstadoOrden.Entregado, EstadoOrden.Cancelado };

    public static bool EsValida(EstadoOrden actual, EstadoOrden nuevo) => Siguientes[actual].Contains(nuevo);

    public static IEnumerable<EstadoOrden> SiguientesDe(EstadoOrden actual) => Siguientes[actual];

    public static bool EsTerminal(EstadoOrden e) => e is EstadoOrden.Entregado or EstadoOrden.Cancelado;
}
