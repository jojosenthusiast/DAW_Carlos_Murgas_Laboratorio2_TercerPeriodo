using AutoFix.Api.Common;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Data;

/// <summary>Aplica migraciones y siembra datos de prueba (un usuario por rol).</summary>
public static class DbInicializador
{
    public static async Task InicializarAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoFixDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInicializador");

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "No se encontraron migraciones. En la carpeta AutoFix.Api ejecuta: " +
                "dotnet ef migrations add InicialAutoFix  y luego  dotnet ef database update");

        await db.Database.MigrateAsync();
        if (await db.Usuarios.AnyAsync()) return;

        logger.LogInformation("Base de datos vacía: sembrando datos de prueba...");

        var carlos = new Mecanico { NombreCompleto = "Carlos Hernández", Especialidad = "Motor y transmisión", Telefono = "7000-1001", Email = "mecanico1@autofix.com" };
        var ana = new Mecanico { NombreCompleto = "Ana Martínez", Especialidad = "Frenos y suspensión", Telefono = "7000-1002", Email = "mecanico2@autofix.com" };
        var jose = new Mecanico { NombreCompleto = "José López", Especialidad = "Sistema eléctrico", Telefono = "7000-1003", Email = "jose.lopez@autofix.com" };

        var maria = new Cliente
        {
            NombreCompleto = "María José Rivas", Dui = "01234567-8", Telefono = "7111-2233",
            Correo = "cliente@autofix.com", Direccion = "Col. Escalón, San Salvador",
            Vehiculos =
            {
                new Vehiculo { Placa = "P123-456", Marca = "Toyota", Modelo = "Corolla", Anio = 2018, Color = "Gris", NumeroChasis = "JTDBR32E520012345" },
                new Vehiculo { Placa = "P789-012", Marca = "Honda", Modelo = "CR-V", Anio = 2021, Color = "Blanco", NumeroChasis = "5J6RW2H89ML001234" }
            }
        };

        db.Mecanicos.AddRange(carlos, ana, jose);
        db.Clientes.Add(maria);
        await db.SaveChangesAsync();

        Usuario Crear(string nombre, string email, string password, string rol,
                      int? clienteId = null, int? mecanicoId = null, IEnumerable<string>? permisos = null)
        {
            var u = new Usuario { NombreCompleto = nombre, Email = email, Rol = rol, ClienteId = clienteId, MecanicoId = mecanicoId };
            u.PasswordHash = AuthService.Hashear(u, password);
            foreach (var p in permisos ?? Permisos.PorDefecto(rol))
                u.Permisos.Add(new UsuarioPermiso { Permiso = p });
            return u;
        }

        db.Usuarios.AddRange(
            Crear("Administrador AutoFix", "admin@autofix.com", "Admin#2026", Roles.Administrador),
            Crear("Laura Recepción", "recepcion@autofix.com", "Recepcion#2026", Roles.Recepcionista),
            // Mismo rol, pero SIN el claim Ordenes.Editar: demuestra autorización por claim (403).
            Crear("Pedro Recepción (limitado)", "recepcion.limitada@autofix.com", "Recepcion#2026", Roles.Recepcionista,
                  permisos: new[] { Permisos.OrdenesCrear }),
            Crear("Roberto Supervisor", "supervisor@autofix.com", "Supervisor#2026", Roles.Supervisor),
            Crear("Sofía Supervisora", "supervisor2@autofix.com", "Supervisor#2026", Roles.Supervisor),
            Crear(carlos.NombreCompleto, "mecanico1@autofix.com", "Mecanico#2026", Roles.Mecanico, mecanicoId: carlos.Id),
            Crear(ana.NombreCompleto, "mecanico2@autofix.com", "Mecanico#2026", Roles.Mecanico, mecanicoId: ana.Id),
            Crear(maria.NombreCompleto, "cliente@autofix.com", "Cliente#2026", Roles.Cliente, clienteId: maria.Id));

        var repuestos = new[]
        {
            new Repuesto { Codigo = "FIL-ACE-001", Nombre = "Filtro de aceite", Descripcion = "Filtro de aceite estándar para motores 1.6-2.4 L", Categoria = "Filtros", Marca = "Bosch", Precio = 8.50m, Existencias = 25, StockMinimo = 5 },
            new Repuesto { Codigo = "PAS-FRE-002", Nombre = "Pastillas de freno delanteras", Descripcion = "Juego de 4 pastillas cerámicas", Categoria = "Frenos", Marca = "Brembo", Precio = 35.00m, Existencias = 10, StockMinimo = 4 },
            new Repuesto { Codigo = "SEN-OXI-003", Nombre = "Sensor de oxígeno", Descripcion = "Sensor O2 universal de 4 cables (escenario A: solo 2 unidades)", Categoria = "Sensores", Marca = "Denso", Precio = 65.00m, Existencias = 2, StockMinimo = 1 },
            new Repuesto { Codigo = "BUJ-IRI-004", Nombre = "Bujía de iridio", Descripcion = "Bujía de larga duración", Categoria = "Encendido", Marca = "NGK", Precio = 12.75m, Existencias = 16, StockMinimo = 8 },
            new Repuesto { Codigo = "ACE-5W30-005", Nombre = "Aceite sintético 5W-30 (litro)", Descripcion = "Aceite de motor sintético", Categoria = "Lubricantes", Marca = "Mobil", Precio = 9.90m, Existencias = 40, StockMinimo = 10 },
            new Repuesto { Codigo = "BAT-12V-006", Nombre = "Batería 12V 60Ah", Descripcion = "Batería libre de mantenimiento", Categoria = "Eléctrico", Marca = "LTH", Precio = 110.00m, Existencias = 3, StockMinimo = 3 }
        };
        db.Repuestos.AddRange(repuestos);
        await db.SaveChangesAsync();

        foreach (var r in repuestos)
        {
            db.Movimientos.Add(new MovimientoInventario
            {
                RepuestoId = r.Id, Tipo = TipoMovimiento.Entrada, Cantidad = r.Existencias,
                ExistenciaAnterior = 0, ExistenciaNueva = r.Existencias,
                Usuario = "sistema", Motivo = "Inventario inicial"
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Datos de prueba creados.");
    }
}
