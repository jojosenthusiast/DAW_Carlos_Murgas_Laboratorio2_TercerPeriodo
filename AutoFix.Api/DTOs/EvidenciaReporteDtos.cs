using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Entities;

namespace AutoFix.Api.DTOs;

/// <summary>Se envía como multipart/form-data.</summary>
public class SubirEvidenciaDto
{
    [Required(ErrorMessage = "Adjunta una imagen JPG, JPEG o PNG.")]
    public IFormFile? Archivo { get; set; }

    [Required(ErrorMessage = "Indica el tipo de evidencia.")]
    public TipoEvidencia? Tipo { get; set; }

    [StringLength(300)]
    public string? Descripcion { get; set; }
}

public class SubirFotoDto
{
    [Required(ErrorMessage = "Adjunta una imagen JPG, JPEG o PNG.")]
    public IFormFile? Archivo { get; set; }
}

public record EvidenciaDto(
    int Id, int OrdenId, string NumeroOrden, TipoEvidencia Tipo, string Ruta, string NombreOriginal,
    long TamanoBytes, string? Descripcion, DateTime FechaSubida, string SubidoPor);

public record ConteoEstadoDto(EstadoOrden Estado, int Cantidad);
public record ConteoServicioDto(TipoServicio Servicio, int Cantidad);

public record DashboardDto(
    string Rol, int TotalClientes, int TotalVehiculos, int OrdenesActivas, int OrdenesFinalizadasMes,
    int PresupuestosPendientes, decimal MontoAprobadoMes, int MecanicosActivos, int AsignacionesHoy,
    List<ConteoEstadoDto> OrdenesPorEstado, List<RepuestoDto> RepuestosEnAlerta, List<OrdenResumenDto> OrdenesRecientes);

public record ReporteOrdenesDto(
    DateTime Desde, DateTime Hasta, int Total, decimal MontoPresupuestado, decimal MontoAprobado,
    List<ConteoEstadoDto> PorEstado, List<ConteoServicioDto> PorServicio, List<OrdenResumenDto> Ordenes);

public record ConsumoRepuestoDto(int RepuestoId, string Codigo, string Nombre, int CantidadSalida);

public record ReporteInventarioDto(
    int TotalRepuestos, int EnAlerta, decimal ValorInventario, int Entradas, int Salidas,
    List<RepuestoDto> Repuestos, List<ConsumoRepuestoDto> MasUtilizados);

public record CargaMecanicoDto(int MecanicoId, string Nombre, bool Activo, int OrdenesActivas, int OrdenesFinalizadas, int AsignacionesFuturas);

public record LogDto(
    long Id, DateTime Fecha, string CorrelationId, string Usuario, string Metodo, string Endpoint,
    int CodigoEstado, long DuracionMs, string? Ip, string? MensajeError, string? DetalleError);
