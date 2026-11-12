using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Common;

namespace AutoFix.Api.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = "";
}

/// <summary>Registro público: crea siempre un usuario con rol Cliente.</summary>
public class RegistroDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [RegularExpression(Validacion.Password, ErrorMessage = Validacion.MensajePassword)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirma la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = "";

    [Required(ErrorMessage = "El DUI es obligatorio.")]
    [RegularExpression(Validacion.Dui, ErrorMessage = Validacion.MensajeDui)]
    public string Dui { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(Validacion.Telefono, ErrorMessage = Validacion.MensajeTelefono)]
    public string Telefono { get; set; } = "";

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(250, MinimumLength = 5, ErrorMessage = "La dirección debe tener entre 5 y 250 caracteres.")]
    public string Direccion { get; set; } = "";
}

public record UsuarioDto(
    int Id, string NombreCompleto, string Email, string Rol, IReadOnlyList<string> Permisos,
    int? ClienteId, int? MecanicoId, bool Activo, DateTime? UltimoAcceso);

public record LoginRespuestaDto(string Token, DateTime ExpiraUtc, UsuarioDto Usuario);

/// <summary>Alta de usuarios internos (solo Administrador).</summary>
public class CrearUsuarioDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3)]
    public string NombreCompleto { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = "El correo no tiene un formato válido."), StringLength(150)]
    public string Email { get; set; } = "";

    [Required, RegularExpression(Validacion.Password, ErrorMessage = Validacion.MensajePassword)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Rol { get; set; } = "";

    /// <summary>Si se omite, se asignan los permisos por defecto del rol.</summary>
    public List<string>? Permisos { get; set; }

    public int? ClienteId { get; set; }
    public int? MecanicoId { get; set; }
}

public class ActualizarPermisosDto
{
    [Required]
    public List<string> Permisos { get; set; } = new();
}

public class CambiarEstadoActivoDto
{
    public bool Activo { get; set; }
}
