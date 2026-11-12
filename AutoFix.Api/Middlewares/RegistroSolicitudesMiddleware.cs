using System.Diagnostics;
using AutoFix.Api.Common;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;

namespace AutoFix.Api.Middlewares;

/// <summary>
/// Escribe un log por cada petición: usuario, método + endpoint, fecha y hora, código HTTP,
/// duración, Correlation ID y excepción (si ocurrió).
/// </summary>
public class RegistroSolicitudesMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RegistroSolicitudesMiddleware> _logger;
    private readonly LogPersistenciaService _persistencia;

    public RegistroSolicitudesMiddleware(RequestDelegate next, ILogger<RegistroSolicitudesMiddleware> logger, LogPersistenciaService persistencia)
    {
        _next = next;
        _logger = logger;
        _persistencia = persistencia;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var inicio = DateTime.Now;
        var cronometro = Stopwatch.StartNew();
        Exception? error = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            error = ex;
            throw; // el middleware de excepciones (más externo) construye la respuesta
        }
        finally
        {
            cronometro.Stop();
            // Si hubo excepción, la respuesta aún no se escribe: calculamos el código que se enviará.
            var status = error is null ? context.Response.StatusCode : MapeadorExcepciones.Mapear(error).Status;
            var usuario = context.User?.Identity?.IsAuthenticated == true
                ? context.User.FindFirst("email")?.Value ?? context.User.Identity?.Name ?? "autenticado"
                : "anónimo";
            var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? "";

            var nivel = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
            _logger.Log(nivel,
                "HTTP {Metodo} {Endpoint} => {Status} en {Duracion} ms | Usuario: {Usuario} | CorrelationId: {CorrelationId}{Error}",
                context.Request.Method, endpoint, status, cronometro.ElapsedMilliseconds, usuario, correlationId,
                error is null ? "" : $" | Excepción: {error.Message}");

            // Solo las peticiones a la API se guardan en la bitácora (no Swagger ni imágenes).
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await _persistencia.RegistrarAsync(new LogSolicitud
                {
                    Fecha = inicio,
                    CorrelationId = correlationId,
                    Usuario = Validacion.Recortar(usuario, 150),
                    Metodo = context.Request.Method,
                    Endpoint = Validacion.Recortar(endpoint, 500),
                    CodigoEstado = status,
                    DuracionMs = cronometro.ElapsedMilliseconds,
                    Ip = context.Connection.RemoteIpAddress?.ToString(),
                    MensajeError = error is null ? null : Validacion.Recortar(error.Message, 1000),
                    DetalleError = error is null ? null : Validacion.Recortar(error.ToString(), 4000)
                });
            }
        }
    }
}
