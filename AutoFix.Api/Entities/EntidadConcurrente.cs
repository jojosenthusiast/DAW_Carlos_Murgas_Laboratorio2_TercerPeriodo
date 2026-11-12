using System.ComponentModel.DataAnnotations;

namespace AutoFix.Api.Entities;

/// <summary>
/// SQLite no genera RowVersion automáticamente: usamos un Guid como token de concurrencia.
/// AutoFixDbContext lo cambia en cada modificación y EF Core lo compara al guardar
/// (UPDATE ... WHERE Version = @original). Si otro usuario ya cambió la fila,
/// EF Core lanza DbUpdateConcurrencyException.
/// </summary>
public abstract class EntidadConcurrente
{
    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();
}
