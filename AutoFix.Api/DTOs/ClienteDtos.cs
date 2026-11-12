using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Common;

namespace AutoFix.Api.DTOs;

public class ClienteGuardarDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "El DUI o identificación es obligatorio.")]
    [RegularExpression(Validacion.Dui, ErrorMessage = Validacion.MensajeDui)]
    public string Dui { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(Validacion.Telefono, ErrorMessage = Validacion.MensajeTelefono)]
    public string Telefono { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(150)]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(250, MinimumLength = 5, ErrorMessage = "La dirección debe tener entre 5 y 250 caracteres.")]
    public string Direccion { get; set; } = "";

    /// <summary>Opcional y solo al crear: permite registrar el cliente con sus vehículos en una sola transacción.</summary>
    public List<VehiculoNuevoDto>? Vehiculos { get; set; }
}

public class VehiculoNuevoDto : IValidatableObject
{
    [Required(ErrorMessage = "La placa es obligatoria.")]
    [RegularExpression(Validacion.Placa, ErrorMessage = Validacion.MensajePlaca)]
    public string Placa { get; set; } = "";

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [StringLength(50, MinimumLength = 2)]
    public string Marca { get; set; } = "";

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    [StringLength(50, MinimumLength = 1)]
    public string Modelo { get; set; } = "";

    [Range(1950, 2100, ErrorMessage = "El año debe estar entre 1950 y el próximo año.")]
    public int Anio { get; set; }

    [Required(ErrorMessage = "El color es obligatorio.")]
    [StringLength(30, MinimumLength = 3)]
    public string Color { get; set; } = "";

    [Required(ErrorMessage = "El número de chasis es obligatorio.")]
    [RegularExpression(Validacion.Chasis, ErrorMessage = Validacion.MensajeChasis)]
    public string NumeroChasis { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Anio > DateTime.Now.Year + 1)
            yield return new ValidationResult($"El año no puede ser mayor que {DateTime.Now.Year + 1}.", new[] { nameof(Anio) });
    }
}

public class VehiculoGuardarDto : VehiculoNuevoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el cliente propietario.")]
    public int ClienteId { get; set; }
}

public record ClienteDto(
    int Id, string NombreCompleto, string Dui, string Telefono, string Correo,
    string Direccion, DateTime FechaRegistro, int CantidadVehiculos);

public record VehiculoDto(
    int Id, string Placa, string Marca, string Modelo, int Anio, string Color, string NumeroChasis,
    int ClienteId, string ClienteNombre, string? FotoRuta, DateTime FechaRegistro);

public record ClienteDetalleDto(ClienteDto Cliente, List<VehiculoDto> Vehiculos, List<OrdenResumenDto> Ordenes);
