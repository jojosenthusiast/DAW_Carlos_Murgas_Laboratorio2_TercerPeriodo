using System.Globalization;

namespace AutoFix.Mvc.Infraestructura;

/// <summary>Textos legibles y colores de estado para las vistas.</summary>
public static class Formato
{
    private static readonly CultureInfo Dolares = CultureInfo.GetCultureInfo("en-US");

    public static readonly (string Valor, string Texto)[] EstadosOrden =
    {
        ("Recibido", "Recibido"), ("EnDiagnostico", "En diagnóstico"), ("Presupuestado", "Presupuestado"),
        ("PendienteAprobacion", "Pendiente de aprobación"), ("Aprobado", "Aprobado"), ("EnReparacion", "En reparación"),
        ("Finalizado", "Finalizado"), ("Entregado", "Entregado"), ("Cancelado", "Cancelado")
    };

    public static readonly (string Valor, string Texto)[] Servicios =
    {
        ("MantenimientoPreventivo", "Mantenimiento preventivo"), ("CambioAceite", "Cambio de aceite"), ("Frenos", "Frenos"),
        ("Suspension", "Suspensión"), ("Motor", "Motor"), ("Transmision", "Transmisión"), ("SistemaElectrico", "Sistema eléctrico"),
        ("AireAcondicionado", "Aire acondicionado"), ("Carroceria", "Carrocería"), ("DiagnosticoGeneral", "Diagnóstico general"), ("Otro", "Otro")
    };

    public static readonly (string Valor, string Texto)[] TiposEvidencia =
    {
        ("Recepcion", "Vehículo al ingresar"), ("Dano", "Daño identificado"),
        ("RepuestoUtilizado", "Repuesto utilizado"), ("ReparacionFinalizada", "Reparación finalizada")
    };

    public static readonly (string Valor, string Texto)[] EstadosPresupuesto =
    {
        ("Borrador", "Borrador"), ("PendienteAprobacion", "Pendiente de aprobación"), ("Aprobado", "Aprobado"), ("Rechazado", "Rechazado")
    };

    private static string Buscar((string Valor, string Texto)[] lista, string? valor) =>
        lista.FirstOrDefault(x => x.Valor == valor).Texto ?? valor ?? "";

    public static string Estado(string? v) => Buscar(EstadosOrden, v);
    public static string Servicio(string? v) => Buscar(Servicios, v);
    public static string Evidencia(string? v) => Buscar(TiposEvidencia, v);
    public static string EstadoPresupuesto(string? v) => Buscar(EstadosPresupuesto, v);

    /// <summary>Clase CSS de la "etiqueta" de estado.</summary>
    public static string ClaseEstado(string? v) => v switch
    {
        "Recibido" or "Borrador" => "tag-gris",
        "EnDiagnostico" => "tag-azul",
        "Presupuestado" => "tag-violeta",
        "PendienteAprobacion" => "tag-ambar",
        "Aprobado" => "tag-teal",
        "EnReparacion" => "tag-naranja",
        "Finalizado" => "tag-verde",
        "Entregado" => "tag-verde-oscuro",
        "Cancelado" or "Rechazado" => "tag-rojo",
        _ => "tag-gris"
    };

    public static string Dinero(decimal v) => v.ToString("C2", Dolares);
    public static string Fecha(DateTime f) => f.ToString("dd/MM/yyyy HH:mm");
    public static string Fecha(DateTime? f) => f.HasValue ? Fecha(f.Value) : "—";
    public static string Dia(DateTime f) => f.ToString("dd/MM/yyyy");
    public static string Tamano(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{bytes / 1024d:0} KB";
    public static string RolTexto(string rol) => rol == Roles.Mecanico ? "Mecánico" : rol;
}

/// <summary>Construye URLs absolutas hacia las imágenes servidas por la API.</summary>
public class RutasApi
{
    private readonly string _base;
    public RutasApi(IConfiguration config) => _base = (config["Api:BaseUrl"] ?? "").TrimEnd('/') + "/";
    public string? Imagen(string? ruta) => string.IsNullOrWhiteSpace(ruta) ? null : _base + ruta.TrimStart('/');
}
