using Microsoft.AspNetCore.Mvc;

namespace AutoFix.Mvc.Infraestructura;

public abstract class ControladorBase : Controller
{
    protected ApiCliente Api => HttpContext.RequestServices.GetRequiredService<ApiCliente>();

    protected void Exito(string mensaje) => TempData["Exito"] = mensaje;
    protected void Error(string mensaje) => TempData["Error"] = mensaje;

    /// <summary>Copia los errores de validación de la API al formulario.</summary>
    protected void CargarErrores(ApiException ex)
    {
        if (ex.Errores is not null)
        {
            foreach (var (campo, mensajes) in ex.Errores)
            {
                var clave = ModelState.ContainsKey(campo) ? campo : string.Empty;
                foreach (var m in mensajes) ModelState.AddModelError(clave, m);
            }
        }
        ModelState.AddModelError(string.Empty, ex.MensajeCompleto);
    }

    /// <summary>Errores esperados en formularios (validación, conflicto, no encontrado).</summary>
    protected static bool EsErrorDeFormulario(ApiException ex) => ex.Status is 400 or 404 or 409;

    protected static string? ValidarImagen(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0) return "Selecciona una imagen.";
        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png")) return "Solo se permiten JPG, JPEG y PNG.";
        if (archivo.Length > 5 * 1024 * 1024) return "La imagen no puede superar 5 MB.";
        return null;
    }
}
