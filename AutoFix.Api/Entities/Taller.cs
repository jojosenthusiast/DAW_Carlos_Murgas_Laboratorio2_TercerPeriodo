namespace AutoFix.Api.Entities;

public class Mecanico : EntidadConcurrente
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Especialidad { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Activo { get; set; } = true;
    /// <summary>Se modifica en cada asignación para cambiar el token Version (escenario B).</summary>
    public DateTime? UltimaAsignacion { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public List<AsignacionMecanico> Asignaciones { get; set; } = new();
}

/// <summary>Bloque de horario en que un mecánico trabaja una orden.</summary>
public class AsignacionMecanico
{
    public int Id { get; set; }
    public int MecanicoId { get; set; }
    public Mecanico? Mecanico { get; set; }
    public int OrdenId { get; set; }
    public OrdenReparacion? Orden { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public bool Activa { get; set; } = true;
    public string AsignadoPor { get; set; } = "";
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}

public class OrdenReparacion : EntidadConcurrente
{
    public int Id { get; set; }
    public string NumeroOrden { get; set; } = "";

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }

    public TipoServicio TipoServicio { get; set; }
    public DateTime FechaIngreso { get; set; } = DateTime.Now;
    public DateTime FechaEstimadaFinalizacion { get; set; }
    public string DescripcionProblema { get; set; } = "";
    public string? Diagnostico { get; set; }

    public int? MecanicoId { get; set; }
    public Mecanico? Mecanico { get; set; }

    public EstadoOrden Estado { get; set; } = EstadoOrden.Recibido;
    public DateTime? FechaFinalizacion { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public string? MotivoCancelacion { get; set; }

    public string CreadoPor { get; set; } = "";
    public DateTime? FechaActualizacion { get; set; }
    public string? ActualizadoPor { get; set; }

    public Presupuesto? Presupuesto { get; set; }
    public List<Evidencia> Evidencias { get; set; } = new();
    public List<HistorialEstadoOrden> Historial { get; set; } = new();
    public List<AsignacionMecanico> Asignaciones { get; set; } = new();
}

/// <summary>Seguimiento de la orden (quién cambió qué y cuándo).</summary>
public class HistorialEstadoOrden
{
    public int Id { get; set; }
    public int OrdenId { get; set; }
    public OrdenReparacion? Orden { get; set; }
    public EstadoOrden? EstadoAnterior { get; set; }
    public EstadoOrden EstadoNuevo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = "";
    public string? Comentario { get; set; }
}

/// <summary>Solo se guarda la RUTA de la imagen; el archivo vive en wwwroot/uploads.</summary>
public class Evidencia
{
    public int Id { get; set; }
    public int OrdenId { get; set; }
    public OrdenReparacion? Orden { get; set; }
    public TipoEvidencia Tipo { get; set; }
    public string Ruta { get; set; } = "";
    public string NombreOriginal { get; set; } = "";
    public long TamanoBytes { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaSubida { get; set; } = DateTime.Now;
    public string SubidoPor { get; set; } = "";
}
