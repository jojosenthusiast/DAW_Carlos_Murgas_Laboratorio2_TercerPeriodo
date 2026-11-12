using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Entities;

namespace AutoFix.Api.DTOs;

public class PresupuestoGuardarDto
{
    [Range(typeof(decimal), "0", "100000", ErrorMessage = "La mano de obra debe estar entre 0 y 100,000.")]
    public decimal ManoObra { get; set; }

    [StringLength(1000)]
    public string? Observaciones { get; set; }

    public List<DetalleGuardarDto> Detalles { get; set; } = new();

    public Guid? Version { get; set; }
}

public class DetalleGuardarDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el repuesto.")]
    public int RepuestoId { get; set; }

    [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre 1 y 1000.")]
    public int Cantidad { get; set; }
}

public class DecisionPresupuestoDto
{
    public Guid? Version { get; set; }

    [StringLength(500)]
    public string? Comentario { get; set; }
}

public record DetallePresupuestoDto(
    int Id, int RepuestoId, string RepuestoCodigo, string RepuestoNombre,
    int Cantidad, decimal PrecioUnitario, decimal Subtotal, int ExistenciasActuales);

public record PresupuestoDto(
    int Id, int OrdenId, string NumeroOrden, string ClienteNombre, string VehiculoPlaca, EstadoOrden EstadoOrden,
    decimal ManoObra, decimal TotalRepuestos, decimal Total, string? Observaciones,
    EstadoPresupuesto Estado, bool Aprobado,
    DateTime FechaCreacion, string CreadoPor, DateTime? FechaEnvio, DateTime? FechaRespuesta,
    string? RespondidoPor, string? MotivoRechazo, bool InventarioDescontado, Guid Version,
    List<DetallePresupuestoDto> Detalles, List<string> Advertencias);
