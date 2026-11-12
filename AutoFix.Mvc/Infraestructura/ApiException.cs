namespace AutoFix.Mvc.Infraestructura;

/// <summary>Error devuelto por la API (mismo formato ErrorRespuesta).</summary>
public class ApiException : Exception
{
    public int Status { get; }
    public string Mensaje { get; }
    public string? CorrelationId { get; }
    public IDictionary<string, string[]>? Errores { get; }

    public ApiException(int status, string mensaje, string? correlationId, IDictionary<string, string[]>? errores)
        : base(mensaje)
    {
        Status = status;
        Mensaje = mensaje;
        CorrelationId = correlationId;
        Errores = errores;
    }

    /// <summary>Mensaje para mostrar; incluye el Correlation ID en errores que conviene rastrear.</summary>
    public string MensajeCompleto =>
        Status >= 409 && !string.IsNullOrEmpty(CorrelationId) ? $"{Mensaje} (Correlation ID: {CorrelationId})" : Mensaje;
}

public record ApiError(int Status, string? Titulo, string? Mensaje, string? CorrelationId, Dictionary<string, string[]>? Errores);
