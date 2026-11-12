using System.Text.RegularExpressions;

namespace AutoFix.Api.Middlewares;

/// <summary>Asigna un identificador único a cada petición para seguirla de punta a punta.</summary>
public class CorrelationIdMiddleware
{
    public const string Encabezado = "X-Correlation-ID";
    private static readonly Regex Formato = new(@"^[A-Za-z0-9\-]{8,64}$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var recibido = context.Request.Headers[Encabezado].FirstOrDefault();
        // Validación extra: solo se acepta un ID externo con formato seguro (evita inyección en logs).
        var id = recibido is not null && Formato.IsMatch(recibido) ? recibido : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = id;
        context.TraceIdentifier = id;
        context.Response.Headers[Encabezado] = id;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await _next(context);
        }
    }
}
