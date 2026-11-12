using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Common;

namespace AutoFix.Api.DTOs;

public class MecanicoGuardarDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3)]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "La especialidad es obligatoria.")]
    [StringLength(80, MinimumLength = 3)]
    public string Especialidad { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(Validacion.Telefono, ErrorMessage = Validacion.MensajeTelefono)]
    public string Telefono { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = "";

    /// <summary>Solo al crear: si se indica, se crea también su usuario con rol Mecánico.</summary>
    [RegularExpression(Validacion.Password, ErrorMessage = Validacion.MensajePassword)]
    public string? PasswordUsuario { get; set; }

    public Guid? Version { get; set; }
}

public record MecanicoDto(
    int Id, string NombreCompleto, string Especialidad, string Telefono, string Email,
    bool Activo, DateTime? UltimaAsignacion, Guid Version, int AsignacionesActivas, bool TieneUsuario);

public record AsignacionDto(
    int Id, int MecanicoId, string MecanicoNombre, int OrdenId, string NumeroOrden,
    DateTime Inicio, DateTime Fin, bool Activa, string AsignadoPor, DateTime FechaRegistro);

public record DisponibilidadDto(int MecanicoId, string MecanicoNombre, bool Disponible, List<AsignacionDto> Conflictos);
