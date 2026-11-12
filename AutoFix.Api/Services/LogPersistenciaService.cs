using AutoFix.Api.Data;
using AutoFix.Api.Entities;

namespace AutoFix.Api.Services;

/// <summary>
/// Guarda cada log en: (1) archivo diario Logs/autofix-AAAAMMDD.log y (2) tabla LogsSolicitudes
/// (para consultarlos desde la API/MVC). Si la escritura falla, nunca rompe la solicitud.
/// </summary>
public class LogPersistenciaService
{
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LogPersistenciaService> _logger;
    private readonly string _carpeta;

    public LogPersistenciaService(IServiceScopeFactory scopes, IWebHostEnvironment env, ILogger<LogPersistenciaService> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _carpeta = Path.Combine(env.ContentRootPath, "Logs");
        Directory.CreateDirectory(_carpeta);
    }

    public async Task RegistrarAsync(LogSolicitud log)
    {
        await EscribirArchivoAsync(log);
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AutoFixDbContext>();
            db.LogsSolicitudes.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el log {CorrelationId} en la base de datos", log.CorrelationId);
        }
    }

    private async Task EscribirArchivoAsync(LogSolicitud l)
    {
        var linea = $"{l.Fecha:yyyy-MM-dd HH:mm:ss.fff} | {l.CorrelationId} | {l.Usuario} | {l.Metodo} {l.Endpoint} | {l.CodigoEstado} | {l.DuracionMs} ms";
        if (!string.IsNullOrEmpty(l.MensajeError)) linea += $" | ERROR: {Limpiar(l.MensajeError)}";

        await Candado.WaitAsync();
        try
        {
            var archivo = Path.Combine(_carpeta, $"autofix-{DateTime.Now:yyyyMMdd}.log");
            await File.AppendAllTextAsync(archivo, linea + Environment.NewLine);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "No se pudo escribir el archivo de log");
        }
        finally
        {
            Candado.Release();
        }
    }

    // Evita inyección de líneas falsas en el archivo de log.
    private static string Limpiar(string texto) => texto.Replace('\r', ' ').Replace('\n', ' ');
}
