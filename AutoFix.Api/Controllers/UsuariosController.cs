using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Controllers;

/// <summary>Administración de usuarios, roles y claims (solo Administrador).</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Roles.Administrador)]
public class UsuariosController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _actual;

    public UsuariosController(AutoFixDbContext db, IUsuarioActual actual)
    {
        _db = db; _actual = actual;
    }

    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> Listar()
    {
        var usuarios = await _db.Usuarios.AsNoTracking().Include(u => u.Permisos).OrderBy(u => u.Rol).ThenBy(u => u.Email).ToListAsync();
        return usuarios.Select(u => u.ToDto()).ToList();
    }

    [HttpGet("permisos-disponibles")]
    public ActionResult<object> PermisosDisponibles() =>
        Ok(new { Roles = Roles.Todos, Permisos = Permisos.Todos });

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(CrearUsuarioDto dto)
    {
        if (!Roles.Todos.Contains(dto.Rol)) throw new BusinessException($"Rol inválido. Valores permitidos: {string.Join(", ", Roles.Todos)}.");
        var email = Validacion.Correo(dto.Email);
        if (await _db.Usuarios.AnyAsync(u => u.Email == email)) throw new ConflictException("Ya existe un usuario con ese correo.");

        if (dto.Rol == Roles.Cliente)
        {
            if (dto.ClienteId is null || !await _db.Clientes.AnyAsync(c => c.Id == dto.ClienteId))
                throw new BusinessException("Un usuario Cliente debe vincularse a un cliente existente (ClienteId).");
            if (await _db.Usuarios.AnyAsync(u => u.ClienteId == dto.ClienteId))
                throw new ConflictException("Ese cliente ya tiene un usuario.");
        }
        if (dto.Rol == Roles.Mecanico)
        {
            if (dto.MecanicoId is null || !await _db.Mecanicos.AnyAsync(m => m.Id == dto.MecanicoId))
                throw new BusinessException("Un usuario Mecánico debe vincularse a un mecánico existente (MecanicoId).");
            if (await _db.Usuarios.AnyAsync(u => u.MecanicoId == dto.MecanicoId))
                throw new ConflictException("Ese mecánico ya tiene un usuario.");
        }

        var permisos = ValidarPermisos(dto.Permisos ?? Permisos.PorDefecto(dto.Rol).ToList());
        var usuario = new Usuario
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Email = email,
            Rol = dto.Rol,
            ClienteId = dto.Rol == Roles.Cliente ? dto.ClienteId : null,
            MecanicoId = dto.Rol == Roles.Mecanico ? dto.MecanicoId : null,
            Permisos = permisos.Select(p => new UsuarioPermiso { Permiso = p }).ToList()
        };
        usuario.PasswordHash = AuthService.Hashear(usuario, dto.Password);
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created, usuario.ToDto());
    }

    /// <summary>Cambia los claims "permiso" del usuario (aplican en su próximo inicio de sesión).</summary>
    [HttpPut("{id:int}/permisos")]
    public async Task<ActionResult<UsuarioDto>> ActualizarPermisos(int id, ActualizarPermisosDto dto)
    {
        var usuario = await _db.Usuarios.Include(u => u.Permisos).FirstOrDefaultAsync(u => u.Id == id)
                      ?? throw new NotFoundException("El usuario no existe.");
        if (usuario.Rol == Roles.Cliente && dto.Permisos.Count > 0)
            throw new BusinessException("Los usuarios con rol Cliente no reciben permisos de gestión.");

        var permisos = ValidarPermisos(dto.Permisos);
        _db.UsuarioPermisos.RemoveRange(usuario.Permisos);
        usuario.Permisos = permisos.Select(p => new UsuarioPermiso { UsuarioId = usuario.Id, Permiso = p }).ToList();
        await _db.SaveChangesAsync();
        return usuario.ToDto();
    }

    [HttpPatch("{id:int}/estado")]
    public async Task<ActionResult<UsuarioDto>> CambiarEstado(int id, CambiarEstadoActivoDto dto)
    {
        var usuario = await _db.Usuarios.Include(u => u.Permisos).FirstOrDefaultAsync(u => u.Id == id)
                      ?? throw new NotFoundException("El usuario no existe.");
        if (usuario.Id == _actual.Id && !dto.Activo)
            throw new BusinessException("No puedes desactivar tu propia cuenta.");
        usuario.Activo = dto.Activo;
        if (dto.Activo) { usuario.IntentosFallidos = 0; usuario.BloqueadoHasta = null; }
        await _db.SaveChangesAsync();
        return usuario.ToDto();
    }

    private static List<string> ValidarPermisos(IEnumerable<string> permisos)
    {
        var lista = permisos.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();
        var invalidos = lista.Where(p => !Permisos.Todos.Contains(p)).ToList();
        if (invalidos.Count > 0)
            throw new BusinessException($"Permisos inválidos: {string.Join(", ", invalidos)}. Válidos: {string.Join(", ", Permisos.Todos)}.");
        return lista;
    }
}
