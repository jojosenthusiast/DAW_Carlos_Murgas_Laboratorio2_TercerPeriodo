using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Common;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    protected AppException(string mensaje, int statusCode) : base(mensaje) => StatusCode = statusCode;
}

public class BusinessException : AppException { public BusinessException(string m) : base(m, 400) { } }
public class NoAutorizadoException : AppException { public NoAutorizadoException(string m) : base(m, 401) { } }
public class ForbiddenException : AppException { public ForbiddenException(string m) : base(m, 403) { } }
public class NotFoundException : AppException { public NotFoundException(string m) : base(m, 404) { } }
public class ConflictException : AppException { public ConflictException(string m) : base(m, 409) { } }

/// <summary>Formato único de error que devuelve la API (nunca expone detalles internos).</summary>
public record ErrorRespuesta(
    int Status,
    string Titulo,
    string Mensaje,
    string? CorrelationId,
    IDictionary<string, string[]>? Errores = null);

/// <summary>Traduce cualquier excepción a código HTTP + mensaje claro para el usuario.</summary>
public static class MapeadorExcepciones
{
    public static (int Status, string Titulo, string Mensaje) Mapear(Exception ex)
    {
        if (ex is AppException app)
            return (app.StatusCode, Titulo(app.StatusCode), app.Message);

        if (ex is DbUpdateConcurrencyException)
            return (409, Titulo(409), "Otro usuario modificó este registro al mismo tiempo. Recarga los datos e intenta de nuevo.");

        var sqlite = BuscarSqlite(ex);
        if (sqlite is not null)
            return MapearSqlite(sqlite);

        if (ex is BadHttpRequestException bad)
            return (bad.StatusCode, Titulo(bad.StatusCode), bad.StatusCode == 413
                ? "La solicitud supera el tamaño máximo permitido (las imágenes no pueden superar 5 MB)."
                : "La solicitud no tiene un formato válido.");

        if (ex is InvalidDataException)
            return (400, Titulo(400), "El contenido enviado no es válido.");

        return (500, Titulo(500), "Ocurrió un error inesperado. Comunica el Correlation ID al administrador.");
    }

    private static SqliteException? BuscarSqlite(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is SqliteException s) return s;
            ex = ex.InnerException;
        }
        return null;
    }

    private static (int, string, string) MapearSqlite(SqliteException s)
    {
        var msg = s.Message ?? "";
        if (s.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            if (msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
                return (409, Titulo(409), "Ya existe un registro con esos datos únicos (placa, DUI, correo, código u orden activa duplicada).");
            if (msg.Contains("CHECK", StringComparison.OrdinalIgnoreCase))
                return (400, Titulo(400), "La operación viola una regla del negocio (por ejemplo, dejaría existencias negativas).");
            if (msg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
                return (409, Titulo(409), "El registro está relacionado con otros datos y no puede modificarse o eliminarse.");
            return (400, Titulo(400), "Los datos no cumplen las restricciones de la base de datos.");
        }
        if (s.SqliteErrorCode is 5 or 6) // SQLITE_BUSY / SQLITE_LOCKED
            return (409, Titulo(409), "La base de datos está ocupada procesando otra operación simultánea. Intenta de nuevo.");
        return (500, Titulo(500), "Error al acceder a la base de datos. Comunica el Correlation ID al administrador.");
    }

    public static string Titulo(int status) => status switch
    {
        400 => "Solicitud inválida",
        401 => "No autorizado",
        403 => "Prohibido",
        404 => "No encontrado",
        409 => "Conflicto",
        413 => "Contenido demasiado grande",
        429 => "Demasiadas solicitudes",
        _ => "Error interno"
    };
}
