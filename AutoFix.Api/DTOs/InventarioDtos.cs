using System.ComponentModel.DataAnnotations;
using AutoFix.Api.Common;
using AutoFix.Api.Entities;

namespace AutoFix.Api.DTOs;

public class RepuestoGuardarDto
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [RegularExpression(Validacion.CodigoRepuesto, ErrorMessage = Validacion.MensajeCodigo)]
    public string Codigo { get; set; } = "";

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3)]
    public string Nombre { get; set; } = "";

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [StringLength(60, MinimumLength = 2)]
    public string Categoria { get; set; } = "";

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [StringLength(60, MinimumLength = 2)]
    public string Marca { get; set; } = "";

    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "El precio debe estar entre 0.01 y 100,000.")]
    public decimal Precio { get; set; }

    /// <summary>Solo se usa al crear (existencia inicial). Después, solo cambia con entradas y salidas.</summary>
    [Range(0, 100000, ErrorMessage = "Las existencias no pueden ser negativas.")]
    public int Existencias { get; set; }

    [Range(0, 100000, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public int StockMinimo { get; set; }

    public Guid? Version { get; set; }
}

public class MovimientoCrearDto
{
    [Range(1, 100000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int Cantidad { get; set; }

    [Required(ErrorMessage = "Indica el motivo del movimiento.")]
    [StringLength(250, MinimumLength = 3)]
    public string Motivo { get; set; } = "";

    public int? OrdenId { get; set; }

    /// <summary>Versión del repuesto que vio el usuario (concurrencia optimista).</summary>
    public Guid? Version { get; set; }
}

public record RepuestoDto(
    int Id, string Codigo, string Nombre, string Descripcion, string Categoria, string Marca,
    decimal Precio, int Existencias, int StockMinimo, string? FotoRuta, bool Activo, bool EnAlerta, Guid Version);

public record MovimientoDto(
    int Id, int RepuestoId, string RepuestoCodigo, string RepuestoNombre, TipoMovimiento Tipo,
    int Cantidad, int ExistenciaAnterior, int ExistenciaNueva, DateTime Fecha, string Usuario,
    string Motivo, int? OrdenId, string? NumeroOrden);

public record MovimientoResultadoDto(MovimientoDto Movimiento, RepuestoDto Repuesto, bool AlertaStockMinimo, string? MensajeAlerta);
