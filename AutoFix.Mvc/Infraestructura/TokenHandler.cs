using System.Net.Http.Headers;

namespace AutoFix.Mvc.Infraestructura;

/// <summary>Adjunta el JWT (guardado en la cookie de sesión) y el Correlation ID a cada petición a la API.</summary>
public class TokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    public TokenHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var contexto = _accessor.HttpContext;
        if (contexto is not null)
        {
            var token = contexto.User.FindFirst(Claves.Token)?.Value;
            if (!string.IsNullOrEmpty(token) && request.Headers.Authorization is null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Un mismo Correlation ID para todas las llamadas que genera una página del MVC.
            if (contexto.Items["CorrelationId"] is not string correlation)
            {
                correlation = Guid.NewGuid().ToString();
                contexto.Items["CorrelationId"] = correlation;
            }
            if (!request.Headers.Contains("X-Correlation-ID"))
                request.Headers.Add("X-Correlation-ID", correlation);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
