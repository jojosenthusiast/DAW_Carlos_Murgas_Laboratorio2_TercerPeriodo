using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoFix.Mvc.Infraestructura;

/// <summary>
/// Único punto de acceso a datos del MVC: todo pasa por HttpClient hacia AutoFix.Api.
/// El JWT y el Correlation ID se agregan en TokenHandler.
/// </summary>
public class ApiCliente
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public ApiCliente(IHttpClientFactory factory) => _http = factory.CreateClient("AutoFixApi");

    public async Task<T> GetAsync<T>(string url) => await Leer<T>(await _http.GetAsync(url));

    public async Task<T> PostAsync<T>(string url, object? cuerpo = null) =>
        await Leer<T>(await _http.PostAsJsonAsync(url, cuerpo ?? new { }, Json));

    public async Task PostAsync(string url, object? cuerpo = null) =>
        await AsegurarExito(await _http.PostAsJsonAsync(url, cuerpo ?? new { }, Json));

    public async Task<T> PutAsync<T>(string url, object cuerpo) =>
        await Leer<T>(await _http.PutAsJsonAsync(url, cuerpo, Json));

    public async Task<T> PatchAsync<T>(string url, object cuerpo) =>
        await Leer<T>(await _http.PatchAsJsonAsync(url, cuerpo, Json));

    public async Task DeleteAsync(string url) => await AsegurarExito(await _http.DeleteAsync(url));

    /// <summary>Envía una imagen como multipart/form-data (IFormFile en la API).</summary>
    public async Task<T> PostArchivoAsync<T>(string url, IFormFile archivo, IDictionary<string, string?>? campos = null)
    {
        using var contenido = new MultipartFormDataContent();
        await using var flujo = archivo.OpenReadStream();
        var parteArchivo = new StreamContent(flujo);
        if (MediaTypeHeaderValue.TryParse(archivo.ContentType, out var tipo))
            parteArchivo.Headers.ContentType = tipo;
        contenido.Add(parteArchivo, "Archivo", Path.GetFileName(archivo.FileName));

        if (campos is not null)
            foreach (var (clave, valor) in campos)
                if (!string.IsNullOrWhiteSpace(valor))
                    contenido.Add(new StringContent(valor), clave);

        return await Leer<T>(await _http.PostAsync(url, contenido));
    }

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta)
    {
        await AsegurarExito(respuesta);
        if (respuesta.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task AsegurarExito(HttpResponseMessage respuesta)
    {
        if (respuesta.IsSuccessStatusCode) return;

        ApiError? error = null;
        try { error = await respuesta.Content.ReadFromJsonAsync<ApiError>(Json); }
        catch (Exception) { /* la respuesta no era JSON */ }

        var correlation = error?.CorrelationId
                          ?? (respuesta.Headers.TryGetValues("X-Correlation-ID", out var v) ? v.FirstOrDefault() : null);
        var mensaje = error?.Mensaje ?? (int)respuesta.StatusCode switch
        {
            401 => "Tu sesión expiró. Inicia sesión nuevamente.",
            403 => "No tienes permiso para realizar esta acción.",
            404 => "El recurso solicitado no existe.",
            _ => $"La API respondió con el código {(int)respuesta.StatusCode}."
        };
        throw new ApiException((int)respuesta.StatusCode, mensaje, correlation, error?.Errores);
    }
}
