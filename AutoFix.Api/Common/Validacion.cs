using System.Text.RegularExpressions;

namespace AutoFix.Api.Common;

public static class Validacion
{
    public const string Dui = @"^[A-Za-z0-9\-]{5,20}$";
    public const string MensajeDui = "El DUI o identificación solo admite letras, números y guiones (5 a 20 caracteres). Ej.: 01234567-8";
    public const string Telefono = @"^[0-9+()\-\s]{8,20}$";
    public const string MensajeTelefono = "Teléfono inválido (8 a 20 dígitos; se permiten +, -, espacios y paréntesis).";
    public const string Placa = @"^[A-Za-z0-9\- ]{3,12}$";
    public const string MensajePlaca = "Placa inválida (3 a 12 caracteres: letras, números, guion o espacio).";
    public const string Chasis = @"^[A-Za-z0-9\-]{5,30}$";
    public const string MensajeChasis = "Número de chasis inválido (5 a 30 caracteres alfanuméricos).";
    public const string CodigoRepuesto = @"^[A-Za-z0-9\-]{3,30}$";
    public const string MensajeCodigo = "Código inválido (3 a 30 caracteres: letras, números o guion).";
    public const string Password = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,64}$";
    public const string MensajePassword = "La contraseña debe tener entre 8 y 64 caracteres e incluir mayúscula, minúscula, número y símbolo.";

    /// <summary>Quita espacios repetidos y pasa a mayúsculas (placas, chasis, DUI, códigos).</summary>
    public static string Normalizar(string valor) => Regex.Replace(valor.Trim(), @"\s+", " ").ToUpperInvariant();

    public static string Correo(string valor) => valor.Trim().ToLowerInvariant();

    /// <summary>Guarda todas las fechas en hora local del taller (evita mezclar UTC y local en SQLite).</summary>
    public static DateTime ALocal(DateTime fecha) => fecha.Kind == DateTimeKind.Utc ? fecha.ToLocalTime() : fecha;

    public static string Recortar(string? texto, int max) =>
        string.IsNullOrEmpty(texto) ? "" : (texto.Length <= max ? texto : texto[..max]);
}
