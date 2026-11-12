using AutoFix.Api.Common;

namespace AutoFix.Api.Middlewares;

/// <summary>Atrapa cualquier error y responde con un mensaje claro; nunca muestra el error interno.</summary>
public class ExcepcionesMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExcepcionesMiddleware> _logger;

    public ExcepcionesMiddleware(RequestDelegate next, ILogger<ExcepcionesMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, titulo, mensaje) = MapeadorExcepciones.Mapear(ex);
            var correlationId = context.Items["CorrelationId"]?.ToString();

            if (status >= 500)
                _logger.LogError(ex, "Error no controlado en {Metodo} {Ruta}", context.Request.Method, context.Request.Path);
            else
                _logger.LogWarning("Solicitud rechazada ({Status}) en {Metodo} {Ruta}: {Mensaje}", status, context.Request.Method, context.Request.Path, ex.Message);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = status;
            if (correlationId is not null) context.Response.Headers[CorrelationIdMiddleware.Encabezado] = correlationId;
            await context.Response.WriteAsJsonAsync(new ErrorRespuesta(status, titulo, mensaje, correlationId));
        }
    }
}
