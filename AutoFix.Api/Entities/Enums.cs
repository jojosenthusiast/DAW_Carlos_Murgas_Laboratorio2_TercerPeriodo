namespace AutoFix.Api.Entities;

public enum EstadoOrden
{
    Recibido = 1,
    EnDiagnostico = 2,
    Presupuestado = 3,
    PendienteAprobacion = 4,
    Aprobado = 5,
    EnReparacion = 6,
    Finalizado = 7,
    Entregado = 8,
    Cancelado = 9
}

public enum TipoServicio
{
    MantenimientoPreventivo = 1,
    CambioAceite = 2,
    Frenos = 3,
    Suspension = 4,
    Motor = 5,
    Transmision = 6,
    SistemaElectrico = 7,
    AireAcondicionado = 8,
    Carroceria = 9,
    DiagnosticoGeneral = 10,
    Otro = 11
}

public enum EstadoPresupuesto { Borrador = 1, PendienteAprobacion = 2, Aprobado = 3, Rechazado = 4 }

public enum TipoMovimiento { Entrada = 1, Salida = 2 }

public enum TipoEvidencia { Recepcion = 1, Dano = 2, RepuestoUtilizado = 3, ReparacionFinalizada = 4 }
