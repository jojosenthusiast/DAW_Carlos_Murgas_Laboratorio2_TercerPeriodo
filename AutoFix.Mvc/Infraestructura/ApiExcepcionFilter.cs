using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AutoFix.Mvc.Infraestructura;

/// <summary>
/// Traduce errores de la API a la interfaz: 401 -> login; 403 -> acceso denegado;
/// otros -> mensaje claro. Si la API no responde, lo indica en lugar de mostrar un error técnico.
/// </summary>
public class ApiExcepcionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<ApiExcepcionFilter> _logger;
    private readonly ITempDataDictionaryFactory _tempData;
    private readonly IModelMetadataProvider _metadata;

    public ApiExcepcionFilter(ILogger<ApiExcepcionFilter> logger, ITempDataDictionaryFactory tempData, IModelMetadataProvider metadata)
    {
        _logger = logger; _tempData = tempData; _metadata = metadata;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;
        switch (context.Exception)
        {
            case ApiException ex when ex.Status == 401:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = new RedirectToActionResult("Login", "Cuenta",
                    new { expirado = true, returnUrl = http.Request.Path + http.Request.QueryString });
                break;

            case ApiException ex when ex.Status == 403:
                Mensaje(http, ex.Mensaje);
                context.Result = new RedirectToActionResult("AccesoDenegado", "Cuenta", null);
                break;

            case ApiException ex:
                if (HttpMethods.IsGet(http.Request.Method))
                    context.Result = Vista(context, ex.MensajeCompleto, ex.Status);
                else
                {
                    Mensaje(http, ex.MensajeCompleto);
                    context.Result = new RedirectResult(Volver(http));
                }
                break;

            case HttpRequestException hre:
                _logger.LogError(hre, "No se pudo conectar con la API");
                context.Result = Vista(context, "No fue posible comunicarse con AutoFix.Api. Verifica que la API esté en ejecución.", 503);
                break;

            case TaskCanceledException:
                context.Result = Vista(context, "La API tardó demasiado en responder. Intenta de nuevo.", 504);
                break;

            default:
                return;
        }
        context.ExceptionHandled = true;
    }

    private void Mensaje(HttpContext http, string texto)
    {
        var temp = _tempData.GetTempData(http);
        temp["Error"] = texto;
        temp.Save();
    }

    private static string Volver(HttpContext http)
    {
        var referer = http.Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host)
            return uri.PathAndQuery;
        return "/";
    }

    private IActionResult Vista(ExceptionContext context, string mensaje, int status) => new ViewResult
    {
        ViewName = "ErrorApi",
        StatusCode = status,
        ViewData = new ViewDataDictionary(_metadata, context.ModelState) { Model = new ErrorApiVm(mensaje, status) }
    };
}
