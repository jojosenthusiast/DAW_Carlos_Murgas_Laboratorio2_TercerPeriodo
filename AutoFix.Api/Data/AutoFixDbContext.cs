using AutoFix.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Data;

public class AutoFixDbContext : DbContext
{
    public AutoFixDbContext(DbContextOptions<AutoFixDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioPermiso> UsuarioPermisos => Set<UsuarioPermiso>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Mecanico> Mecanicos => Set<Mecanico>();
    public DbSet<AsignacionMecanico> Asignaciones => Set<AsignacionMecanico>();
    public DbSet<OrdenReparacion> Ordenes => Set<OrdenReparacion>();
    public DbSet<HistorialEstadoOrden> HistorialEstados => Set<HistorialEstadoOrden>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>();
    public DbSet<DetallePresupuesto> DetallesPresupuesto => Set<DetallePresupuesto>();
    public DbSet<Repuesto> Repuestos => Set<Repuesto>();
    public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();
    public DbSet<LogSolicitud> LogsSolicitudes => Set<LogSolicitud>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Usuario>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.NombreCompleto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Rol).HasMaxLength(30).IsRequired();
            e.HasOne(x => x.Cliente).WithOne().HasForeignKey<Usuario>(x => x.ClienteId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Mecanico).WithOne().HasForeignKey<Usuario>(x => x.MecanicoId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<UsuarioPermiso>(e =>
        {
            e.Property(x => x.Permiso).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.UsuarioId, x.Permiso }).IsUnique();
            e.HasOne(x => x.Usuario).WithMany(u => u.Permisos).HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Cliente>(e =>
        {
            e.Property(x => x.NombreCompleto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Dui).HasMaxLength(20).IsRequired();
            e.Property(x => x.Telefono).HasMaxLength(20).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(150).IsRequired();
            e.Property(x => x.Direccion).HasMaxLength(250).IsRequired();
            e.HasIndex(x => x.Dui).IsUnique();
            e.HasIndex(x => x.Correo).IsUnique();
            // Un cliente tiene muchos vehículos (uno a muchos).
            e.HasMany(x => x.Vehiculos).WithOne(v => v.Cliente).HasForeignKey(v => v.ClienteId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Vehiculo>(e =>
        {
            e.Property(x => x.Placa).HasMaxLength(12).IsRequired();
            e.Property(x => x.Marca).HasMaxLength(50).IsRequired();
            e.Property(x => x.Modelo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Color).HasMaxLength(30).IsRequired();
            e.Property(x => x.NumeroChasis).HasMaxLength(30).IsRequired();
            e.Property(x => x.FotoRuta).HasMaxLength(300);
            e.HasIndex(x => x.Placa).IsUnique();
            e.HasIndex(x => x.NumeroChasis).IsUnique();
        });

        mb.Entity<Mecanico>(e =>
        {
            e.Property(x => x.NombreCompleto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Especialidad).HasMaxLength(80).IsRequired();
            e.Property(x => x.Telefono).HasMaxLength(20).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        mb.Entity<AsignacionMecanico>(e =>
        {
            e.HasOne(x => x.Mecanico).WithMany(m => m.Asignaciones).HasForeignKey(x => x.MecanicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Orden).WithMany(o => o.Asignaciones).HasForeignKey(x => x.OrdenId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.MecanicoId, x.Activa, x.Inicio, x.Fin });
            e.ToTable(t => t.HasCheckConstraint("CK_Asignacion_Horario", "\"Fin\" > \"Inicio\""));
        });

        mb.Entity<OrdenReparacion>(e =>
        {
            e.Property(x => x.NumeroOrden).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.NumeroOrden).IsUnique();
            e.Property(x => x.DescripcionProblema).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Diagnostico).HasMaxLength(2000);
            e.Property(x => x.MotivoCancelacion).HasMaxLength(500);
            e.HasOne(x => x.Cliente).WithMany(c => c.Ordenes).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Vehiculo).WithMany(v => v.Ordenes).HasForeignKey(x => x.VehiculoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Mecanico).WithMany().HasForeignKey(x => x.MecanicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Presupuesto).WithOne(p => p.Orden).HasForeignKey<Presupuesto>(p => p.OrdenId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Estado);
            // Problema 1: no puede haber dos órdenes ACTIVAS para el mismo vehículo y servicio.
            // Índice único parcial (8 = Entregado, 9 = Cancelado no cuentan como activas).
            e.HasIndex(x => new { x.VehiculoId, x.TipoServicio })
             .IsUnique()
             .HasFilter("\"Estado\" NOT IN (8, 9)")
             .HasDatabaseName("IX_Ordenes_OrdenActivaUnica");
        });

        mb.Entity<HistorialEstadoOrden>(e =>
        {
            e.Property(x => x.Comentario).HasMaxLength(500);
            e.HasOne(x => x.Orden).WithMany(o => o.Historial).HasForeignKey(x => x.OrdenId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Evidencia>(e =>
        {
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.Property(x => x.NombreOriginal).HasMaxLength(200);
            e.Property(x => x.Descripcion).HasMaxLength(300);
            e.HasOne(x => x.Orden).WithMany(o => o.Evidencias).HasForeignKey(x => x.OrdenId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Presupuesto>(e =>
        {
            e.Property(x => x.ManoObra).HasPrecision(18, 2);
            e.Property(x => x.TotalRepuestos).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.HasMany(x => x.Detalles).WithOne(d => d.Presupuesto).HasForeignKey(d => d.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DetallePresupuesto>(e =>
        {
            e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.HasOne(x => x.Repuesto).WithMany().HasForeignKey(x => x.RepuestoId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Detalle_Cantidad", "\"Cantidad\" > 0"));
        });

        mb.Entity<Repuesto>(e =>
        {
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.Categoria).HasMaxLength(60).IsRequired();
            e.Property(x => x.Marca).HasMaxLength(60).IsRequired();
            e.Property(x => x.Precio).HasPrecision(18, 2);
            // Última línea de defensa: la base NUNCA acepta existencias negativas.
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Repuesto_Existencias", "\"Existencias\" >= 0");
                t.HasCheckConstraint("CK_Repuesto_StockMinimo", "\"StockMinimo\" >= 0");
            });
        });

        mb.Entity<MovimientoInventario>(e =>
        {
            e.Property(x => x.Motivo).HasMaxLength(250).IsRequired();
            e.HasOne(x => x.Repuesto).WithMany().HasForeignKey(x => x.RepuestoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Orden).WithMany().HasForeignKey(x => x.OrdenId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Fecha);
            e.ToTable(t => t.HasCheckConstraint("CK_Movimiento_Cantidad", "\"Cantidad\" > 0"));
        });

        mb.Entity<LogSolicitud>(e =>
        {
            e.ToTable("LogsSolicitudes");
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            e.Property(x => x.Usuario).HasMaxLength(150);
            e.Property(x => x.Metodo).HasMaxLength(10);
            e.Property(x => x.Endpoint).HasMaxLength(500);
            e.HasIndex(x => x.CorrelationId);
            e.HasIndex(x => x.Fecha);
        });
    }

    // Cambiar el token de concurrencia en cada modificación (requisito para SQLite).
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RenovarTokensDeConcurrencia();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RenovarTokensDeConcurrencia();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RenovarTokensDeConcurrencia()
    {
        foreach (var entry in ChangeTracker.Entries<EntidadConcurrente>()
                     .Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.Version = Guid.NewGuid();
        }
    }
}
