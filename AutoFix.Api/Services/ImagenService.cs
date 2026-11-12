using AutoFix.Api.Common;

namespace AutoFix.Api.Services;

public record ArchivoGuardado(string Ruta, string NombreOriginal, long TamanoBytes);

/// <summary>Valida y guarda imágenes en wwwroot/uploads; en la base solo se guarda la ruta.</summary>
public class ImagenService
{
    public const long TamanoMaximo = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] Permitidas = { ".jpg", ".jpeg", ".png" };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImagenService> _logger;

    public ImagenService(IWebHostEnvironment env, ILogger<ImagenService> logger)
    {
        _env = env; _logger = logger;
    }

    private string Raiz => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

    public async Task<ArchivoGuardado> GuardarAsync(IFormFile? archivo, string carpeta)
    {
        if (archivo is null || archivo.Length == 0)
            throw new BusinessException("Debes adjuntar una imagen JPG, JPEG o PNG.");

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!Permitidas.Contains(ext))
            throw new BusinessException("Solo se permiten JPG, JPEG y PNG.");

        if (archivo.Length > TamanoMaximo)
            throw new BusinessException("La imagen no puede superar 5 MB.");

        // Validación extra: la extensión puede mentir; revisamos la "firma" del archivo.
        var cabecera = new byte[8];
        int leidos;
        await using (var lectura = archivo.OpenReadStream())
        {
            leidos = await lectura.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false);
        }
        var esPng = leidos >= 8 && cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47;
        var esJpg = leidos >= 3 && cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF;
        if ((ext == ".png" && !esPng) || (ext is ".jpg" or ".jpeg" && !esJpg))
            throw new BusinessException("El contenido del archivo no corresponde a una imagen JPG/PNG válida.");

        var nombre = $"{Guid.NewGuid()}{ext}";               // nombre único
        var directorio = Path.Combine(Raiz, "uploads", carpeta);
        Directory.CreateDirectory(directorio);
        var rutaFisica = Path.Combine(directorio, nombre);

        await using (var destino = File.Create(rutaFisica))
        {
            await archivo.CopyToAsync(destino);
        }

        var original = Validacion.Recortar(Path.GetFileName(archivo.FileName), 200);
        _logger.LogInformation("Imagen guardada: uploads/{Carpeta}/{Nombre} ({Bytes} bytes)", carpeta, nombre, archivo.Length);
        return new ArchivoGuardado($"uploads/{carpeta}/{nombre}", original, archivo.Length);
    }

    /// <summary>Elimina un archivo previamente guardado (no lanza error si no existe).</summary>
    public void Eliminar(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa)) return;
        try
        {
            var uploads = Path.GetFullPath(Path.Combine(Raiz, "uploads"));
            var completa = Path.GetFullPath(Path.Combine(Raiz, rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));
            if (!completa.StartsWith(uploads, StringComparison.OrdinalIgnoreCase)) return; // evita rutas fuera de uploads
            if (File.Exists(completa)) File.Delete(completa);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la imagen {Ruta}", rutaRelativa);
        }
    }
}
