using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Services;

public class AuthService
{
    private static readonly PasswordHasher<Usuario> Hasher = new();
    private const int MaxIntentos = 5;
    private const int MinutosBloqueo = 5;

    private readonly AutoFixDbContext _db;
    private readonly TokenService _tokens;
    private readonly IUsuarioActual _actual;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AutoFixDbContext db, TokenService tokens, IUsuarioActual actual, ILogger<AuthService> logger)
    {
        _db = db; _tokens = tokens; _actual = actual; _logger = logger;
    }

    public static string Hashear(Usuario u, string password) => Hasher.HashPassword(u, password);

    public async Task<LoginRespuestaDto> LoginAsync(LoginDto dto)
    {
        var email = Validacion.Correo(dto.Email);
        var usuario = await _db.Usuarios.Include(u => u.Permisos).FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null)
        {
            _logger.LogWarning("Intento de inicio de sesión con correo inexistente: {Email}", email);
            throw new NoAutorizadoException("Correo o contraseña incorrectos.");
        }
        if (!usuario.Activo)
            throw new NoAutorizadoException("La cuenta está desactivada. Contacta al administrador.");
        if (usuario.BloqueadoHasta is DateTime bloqueo && bloqueo > DateTime.Now)
            throw new NoAutorizadoException($"Cuenta bloqueada por intentos fallidos. Intenta de nuevo después de las {bloqueo:HH:mm}.");

        var resultado = Hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, dto.Password);
        if (resultado == PasswordVerificationResult.Failed)
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= MaxIntentos)
            {
                usuario.BloqueadoHasta = DateTime.Now.AddMinutes(MinutosBloqueo);
                usuario.IntentosFallidos = 0;
                _logger.LogWarning("Usuario {Email} bloqueado {Minutos} minutos por intentos fallidos", email, MinutosBloqueo);
            }
            await _db.SaveChangesAsync();
            throw new NoAutorizadoException("Correo o contraseña incorrectos.");
        }

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
            usuario.PasswordHash = Hasher.HashPassword(usuario, dto.Password);

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoAcceso = DateTime.Now;
        await _db.SaveChangesAsync();

        var (token, expira) = _tokens.Generar(usuario);
        _logger.LogInformation("Inicio de sesión correcto: {Email} ({Rol})", usuario.Email, usuario.Rol);
        return new LoginRespuestaDto(token, expira, usuario.ToDto());
    }

    /// <summary>Registro público: siempre crea rol Cliente (nadie puede auto-registrarse como personal).</summary>
    public async Task<LoginRespuestaDto> RegistrarClienteAsync(RegistroDto dto)
    {
        var email = Validacion.Correo(dto.Email);
        var dui = Validacion.Normalizar(dto.Dui);

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
            throw new ConflictException("Ya existe una cuenta con ese correo.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Dui == dui);
        if (cliente is not null)
        {
            // El cliente ya fue registrado en recepción: se vincula solo si el correo coincide.
            if (!string.Equals(cliente.Correo, email, StringComparison.OrdinalIgnoreCase))
                throw new ConflictException("El DUI ya está registrado con otro correo. Acude a recepción para actualizar tus datos.");
            if (await _db.Usuarios.AnyAsync(u => u.ClienteId == cliente.Id))
                throw new ConflictException("Este cliente ya tiene una cuenta de acceso.");
        }
        else
        {
            if (await _db.Clientes.AnyAsync(c => c.Correo == email))
                throw new ConflictException("El correo ya está registrado para otro cliente.");
            cliente = new Cliente
            {
                NombreCompleto = dto.NombreCompleto.Trim(),
                Dui = dui,
                Telefono = dto.Telefono.Trim(),
                Correo = email,
                Direccion = dto.Direccion.Trim()
            };
            _db.Clientes.Add(cliente);
            await _db.SaveChangesAsync();
        }

        var usuario = new Usuario
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Email = email,
            Rol = Roles.Cliente,
            ClienteId = cliente.Id,
            UltimoAcceso = DateTime.Now
        };
        usuario.PasswordHash = Hasher.HashPassword(usuario, dto.Password);
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation("Nuevo cliente registrado: {Email}", email);
        var (token, expira) = _tokens.Generar(usuario);
        return new LoginRespuestaDto(token, expira, usuario.ToDto());
    }

    public async Task<UsuarioDto> PerfilAsync()
    {
        var id = _actual.Id ?? throw new NoAutorizadoException("Token sin identificador de usuario.");
        var usuario = await _db.Usuarios.AsNoTracking().Include(u => u.Permisos).FirstOrDefaultAsync(u => u.Id == id)
                      ?? throw new NotFoundException("El usuario ya no existe.");
        return usuario.ToDto();
    }
}
