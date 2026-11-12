using AutoFix.Api.Common;
using AutoFix.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Data;

public static class ConcurrenciaExtensions
{
    /// <summary>
    /// Concurrencia optimista "de punta a punta": el cliente envía la Version que vio al consultar.
    /// 1) Si ya no coincide, alguien modificó el registro antes -> 409.
    /// 2) Se fija como OriginalValue: si otro usuario guarda entre nuestra lectura y nuestro
    ///    SaveChanges, el UPDATE ... WHERE Version = @v no afecta filas -> DbUpdateConcurrencyException.
    /// </summary>
    public static void ValidarVersion(this DbContext db, EntidadConcurrente entidad, Guid? versionEsperada, string nombre)
    {
        if (versionEsperada is null || versionEsperada == Guid.Empty) return;

        if (entidad.Version != versionEsperada.Value)
            throw new ConflictException($"{nombre} fue modificado por otro usuario después de que lo consultaste. Recarga los datos e intenta de nuevo.");

        db.Entry(entidad).Property(nameof(EntidadConcurrente.Version)).OriginalValue = versionEsperada.Value;
    }
}
