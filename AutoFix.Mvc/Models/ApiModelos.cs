namespace AutoFix.Mvc.Models;

// Modelos que reflejan las respuestas JSON de la API (los enums llegan como texto).

public record ErrorApiVm(string Mensaje, int Status);

public record UsuarioVm(int Id, string NombreCompleto, string Email, string Rol, List<string> Permisos,
    int? ClienteId, int? MecanicoId, bool Activo, DateTime? UltimoAcceso);

public record LoginRespuestaVm(string Token, DateTime ExpiraUtc, UsuarioVm Usuario);

public record ClienteVm(int Id, string NombreCompleto, string Dui, string Telefono, string Correo,
    string Direccion, DateTime FechaRegistro, int CantidadVehiculos);

public record VehiculoVm(int Id, string Placa, string Marca, string Modelo, int Anio, string Color, string NumeroChasis,
    int ClienteId, string ClienteNombre, string? FotoRuta, DateTime FechaRegistro);

public record ClienteDetalleVm(ClienteVm Cliente, List<VehiculoVm> Vehiculos, List<OrdenResumenVm> Ordenes);

public record OrdenResumenVm(int Id, string NumeroOrden, string ClienteNombre, string VehiculoPlaca, string VehiculoDescripcion,
    string TipoServicio, string Estado, DateTime FechaIngreso, DateTime FechaEstimadaFinalizacion, string? MecanicoNombre);

public record HistorialVm(string? EstadoAnterior, string EstadoNuevo, DateTime Fecha, string Usuario, string? Comentario);

public record EvidenciaVm(int Id, int OrdenId, string NumeroOrden, string Tipo, string Ruta, string NombreOriginal,
    long TamanoBytes, string? Descripcion, DateTime FechaSubida, string SubidoPor);

public record AsignacionVm(int Id, int MecanicoId, string MecanicoNombre, int OrdenId, string NumeroOrden,
    DateTime Inicio, DateTime Fin, bool Activa, string AsignadoPor, DateTime FechaRegistro);

public record OrdenDetalleVm(
    int Id, string NumeroOrden, int ClienteId, string ClienteNombre, string ClienteTelefono,
    int VehiculoId, string VehiculoPlaca, string VehiculoDescripcion, string? VehiculoFotoRuta,
    string TipoServicio, string Estado, DateTime FechaIngreso, DateTime FechaEstimadaFinalizacion,
    string DescripcionProblema, string? Diagnostico, int? MecanicoId, string? MecanicoNombre,
    DateTime? FechaFinalizacion, DateTime? FechaEntrega, string? MotivoCancelacion, Guid Version,
    int? PresupuestoId, string? EstadoPresupuesto, decimal? TotalPresupuesto,
    List<HistorialVm> Historial, List<EvidenciaVm> Evidencias, List<AsignacionVm> Asignaciones,
    List<string> TransicionesPermitidas);

public record MecanicoVm(int Id, string NombreCompleto, string Especialidad, string Telefono, string Email,
    bool Activo, DateTime? UltimaAsignacion, Guid Version, int AsignacionesActivas, bool TieneUsuario);

public record DisponibilidadVm(int MecanicoId, string MecanicoNombre, bool Disponible, List<AsignacionVm> Conflictos);

public record RepuestoVm(int Id, string Codigo, string Nombre, string Descripcion, string Categoria, string Marca,
    decimal Precio, int Existencias, int StockMinimo, string? FotoRuta, bool Activo, bool EnAlerta, Guid Version);

public record MovimientoVm(int Id, int RepuestoId, string RepuestoCodigo, string RepuestoNombre, string Tipo,
    int Cantidad, int ExistenciaAnterior, int ExistenciaNueva, DateTime Fecha, string Usuario,
    string Motivo, int? OrdenId, string? NumeroOrden);

public record MovimientoResultadoVm(MovimientoVm Movimiento, RepuestoVm Repuesto, bool AlertaStockMinimo, string? MensajeAlerta);

public record DetallePresupuestoVm(int Id, int RepuestoId, string RepuestoCodigo, string RepuestoNombre,
    int Cantidad, decimal PrecioUnitario, decimal Subtotal, int ExistenciasActuales);

public record PresupuestoVm(int Id, int OrdenId, string NumeroOrden, string ClienteNombre, string VehiculoPlaca, string EstadoOrden,
    decimal ManoObra, decimal TotalRepuestos, decimal Total, string? Observaciones, string Estado, bool Aprobado,
    DateTime FechaCreacion, string CreadoPor, DateTime? FechaEnvio, DateTime? FechaRespuesta, string? RespondidoPor,
    string? MotivoRechazo, bool InventarioDescontado, Guid Version, List<DetallePresupuestoVm> Detalles, List<string> Advertencias);

public record ConteoEstadoVm(string Estado, int Cantidad);
public record ConteoServicioVm(string Servicio, int Cantidad);

public record DashboardVm(string Rol, int TotalClientes, int TotalVehiculos, int OrdenesActivas, int OrdenesFinalizadasMes,
    int PresupuestosPendientes, decimal MontoAprobadoMes, int MecanicosActivos, int AsignacionesHoy,
    List<ConteoEstadoVm> OrdenesPorEstado, List<RepuestoVm> RepuestosEnAlerta, List<OrdenResumenVm> OrdenesRecientes);

public record ReporteOrdenesVm(DateTime Desde, DateTime Hasta, int Total, decimal MontoPresupuestado, decimal MontoAprobado,
    List<ConteoEstadoVm> PorEstado, List<ConteoServicioVm> PorServicio, List<OrdenResumenVm> Ordenes);

public record ConsumoRepuestoVm(int RepuestoId, string Codigo, string Nombre, int CantidadSalida);

public record ReporteInventarioVm(int TotalRepuestos, int EnAlerta, decimal ValorInventario, int Entradas, int Salidas,
    List<RepuestoVm> Repuestos, List<ConsumoRepuestoVm> MasUtilizados);

public record CargaMecanicoVm(int MecanicoId, string Nombre, bool Activo, int OrdenesActivas, int OrdenesFinalizadas, int AsignacionesFuturas);

public record LogVm(long Id, DateTime Fecha, string CorrelationId, string Usuario, string Metodo, string Endpoint,
    int CodigoEstado, long DuracionMs, string? Ip, string? MensajeError, string? DetalleError);
