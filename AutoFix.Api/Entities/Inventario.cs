namespace AutoFix.Api.Entities;

public class Repuesto : EntidadConcurrente
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Marca { get; set; } = "";
    public decimal Precio { get; set; }
    public int Existencias { get; set; }
    public int StockMinimo { get; set; }
    public string? FotoRuta { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}

public class MovimientoInventario
{
    public int Id { get; set; }
    public int RepuestoId { get; set; }
    public Repuesto? Repuesto { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public int ExistenciaAnterior { get; set; }
    public int ExistenciaNueva { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int? OrdenId { get; set; }
    public OrdenReparacion? Orden { get; set; }
}

public class Presupuesto : EntidadConcurrente
{
    public int Id { get; set; }
    public int OrdenId { get; set; }
    public OrdenReparacion? Orden { get; set; }
    public decimal ManoObra { get; set; }
    public decimal TotalRepuestos { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public EstadoPresupuesto Estado { get; set; } = EstadoPresupuesto.Borrador;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public string CreadoPor { get; set; } = "";
    public DateTime? FechaEnvio { get; set; }
    public DateTime? FechaRespuesta { get; set; }
    public string? RespondidoPor { get; set; }
    public string? MotivoRechazo { get; set; }
    /// <summary>Evita descontar dos veces y permite devolver existencias si se cancela.</summary>
    public bool InventarioDescontado { get; set; }

    public List<DetallePresupuesto> Detalles { get; set; } = new();
}

public class DetallePresupuesto
{
    public int Id { get; set; }
    public int PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }
    public int RepuestoId { get; set; }
    public Repuesto? Repuesto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

/// <summary>Bitácora de solicitudes HTTP (módulo 8).</summary>
public class LogSolicitud
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string CorrelationId { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string Metodo { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public int CodigoEstado { get; set; }
    public long DuracionMs { get; set; }
    public string? Ip { get; set; }
    public string? MensajeError { get; set; }
    public string? DetalleError { get; set; }
}
