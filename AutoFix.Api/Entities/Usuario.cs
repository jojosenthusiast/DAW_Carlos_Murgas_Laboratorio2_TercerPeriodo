namespace AutoFix.Api.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool Activo { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? UltimoAcceso { get; set; }

    /// <summary>Si el usuario es Cliente, su ficha de cliente.</summary>
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    /// <summary>Si el usuario es Mecánico, su ficha de mecánico.</summary>
    public int? MecanicoId { get; set; }
    public Mecanico? Mecanico { get; set; }

    public List<UsuarioPermiso> Permisos { get; set; } = new();
}

/// <summary>Cada fila es un claim "permiso" que se incluye en el JWT.</summary>
public class UsuarioPermiso
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Permiso { get; set; } = "";
}
