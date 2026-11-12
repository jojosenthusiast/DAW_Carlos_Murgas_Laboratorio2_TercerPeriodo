using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Entities;

namespace AutoFix.Api.DTOs;

public class CrearOrdenDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el cliente.")]
    public int ClienteId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el vehículo.")]
    public int VehiculoId { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo de servicio.")]
    public TipoServicio? TipoServicio { get; set; }

    [Required(ErrorMessage = "Describe el problema reportado.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 1000 caracteres.")]
    public string DescripcionProblema { get; set; } = "";

    /// <summary>Si se omite, se usa la fecha y hora actual.</summary>
    public DateTime? FechaIngreso { get; set; }

    [Required(ErrorMessage = "Indica la fecha estimada de finalización.")]
    public DateTime? FechaEstimadaFinalizacion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var ingreso = FechaIngreso ?? DateTime.Now;
        if (FechaEstimadaFinalizacion.HasValue && FechaEstimadaFinalizacion.Value <= ingreso)
            yield return new ValidationResult("La fecha estimada debe ser posterior a la fecha de ingreso.", new[] { nameof(FechaEstimadaFinalizacion) });
    }
}

public class EditarOrdenDto
{
    [Required(ErrorMessage = "Describe el problema reportado.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 1000 caracteres.")]
    public string DescripcionProblema { get; set; } = "";

    [Required(ErrorMessage = "Indica la fecha estimada de finalización.")]
    public DateTime? FechaEstimadaFinalizacion { get; set; }

    /// <summary>Token de concurrencia que el usuario vio al abrir la orden.</summary>
    public Guid? Version { get; set; }
}

public class DiagnosticoDto
{
    [Required(ErrorMessage = "El diagnóstico es obligatorio.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "El diagnóstico debe tener entre 10 y 2000 caracteres.")]
    public string Diagnostico { get; set; } = "";

    public Guid? Version { get; set; }
}

public class CambiarEstadoDto
{
    [Required(ErrorMessage = "Indica el nuevo estado.")]
    public EstadoOrden? NuevoEstado { get; set; }

    [StringLength(500)]
    public string? Comentario { get; set; }

    public Guid? Version { get; set; }
}

public class AsignarMecanicoDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el mecánico.")]
    public int MecanicoId { get; set; }

    [Required(ErrorMessage = "Indica el inicio de la asignación.")]
    public DateTime? Inicio { get; set; }

    [Required(ErrorMessage = "Indica el fin de la asignación.")]
    public DateTime? Fin { get; set; }

    /// <summary>Versión del mecánico que vio el supervisor (opcional, refuerza la concurrencia optimista).</summary>
    public Guid? VersionMecanico { get; set; }

    /// <summary>Versión de la orden que vio el supervisor (opcional).</summary>
    public Guid? VersionOrden { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Inicio.HasValue && Fin.HasValue)
        {
            if (Fin.Value <= Inicio.Value)
                yield return new ValidationResult("El fin debe ser posterior al inicio.", new[] { nameof(Fin) });
            else if ((Fin.Value - Inicio.Value).TotalHours > 12)
                yield return new ValidationResult("Una asignación no puede durar más de 12 horas; divide el trabajo en bloques.", new[] { nameof(Fin) });
        }
    }
}

public class UsoRepuestoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el repuesto.")]
    public int RepuestoId { get; set; }

    [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre 1 y 1000.")]
    public int Cantidad { get; set; }

    /// <summary>Versión del repuesto que vio el mecánico (escenario A de concurrencia).</summary>
    public Guid? VersionRepuesto { get; set; }

    [StringLength(200)]
    public string? Observacion { get; set; }
}

public record OrdenResumenDto(
    int Id, string NumeroOrden, string ClienteNombre, string VehiculoPlaca, string VehiculoDescripcion,
    TipoServicio TipoServicio, EstadoOrden Estado, DateTime FechaIngreso, DateTime FechaEstimadaFinalizacion,
    string? MecanicoNombre);

public record HistorialDto(EstadoOrden? EstadoAnterior, EstadoOrden EstadoNuevo, DateTime Fecha, string Usuario, string? Comentario);

public record OrdenDetalleDto(
    int Id, string NumeroOrden,
    int ClienteId, string ClienteNombre, string ClienteTelefono,
    int VehiculoId, string VehiculoPlaca, string VehiculoDescripcion, string? VehiculoFotoRuta,
    TipoServicio TipoServicio, EstadoOrden Estado,
    DateTime FechaIngreso, DateTime FechaEstimadaFinalizacion,
    string DescripcionProblema, string? Diagnostico,
    int? MecanicoId, string? MecanicoNombre,
    DateTime? FechaFinalizacion, DateTime? FechaEntrega, string? MotivoCancelacion,
    Guid Version,
    int? PresupuestoId, EstadoPresupuesto? EstadoPresupuesto, decimal? TotalPresupuesto,
    List<HistorialDto> Historial, List<EvidenciaDto> Evidencias, List<AsignacionDto> Asignaciones,
    List<EstadoOrden> TransicionesPermitidas);
