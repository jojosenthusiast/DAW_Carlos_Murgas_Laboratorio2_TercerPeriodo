using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Controllers;

[ApiController]
[Route("api/mecanicos")]
[Authorize(Policy = Politicas.PersonalTaller)]
public class MecanicosController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly AsignacionService _asignaciones;

    public MecanicosController(AutoFixDbContext db, IUsuarioActual usuario, AsignacionService asignaciones)
    {
        _db = db; _usuario = usuario; _asignaciones = asignaciones;
    }

    [HttpGet]
    public async Task<ActionResult<List<MecanicoDto>>> Listar([FromQuery] bool soloActivos = false)
    {
        var q = _db.Mecanicos.AsNoTracking();
        if (soloActivos) q = q.Where(m => m.Activo);
        var lista = await q.OrderBy(m => m.NombreCompleto).ToListAsync();
        return await ConvertirAsync(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MecanicoDto>> Obtener(int id)
    {
        var m = await _db.Mecanicos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El mecánico no existe.");
        return (await ConvertirAsync(new List<Mecanico> { m }))[0];
    }

    [HttpGet("{id:int}/asignaciones")]
    public async Task<ActionResult<List<AsignacionDto>>> Asignaciones(int id, [FromQuery] bool soloActivas = false)
    {
        if (_usuario.EsRol(Roles.Mecanico) && _usuario.MecanicoId != id)
            throw new ForbiddenException("Solo puedes consultar tus propias asignaciones.");
        if (!await _db.Mecanicos.AnyAsync(m => m.Id == id)) throw new NotFoundException("El mecánico no existe.");

        var q = _db.Asignaciones.AsNoTracking().Include(a => a.Orden).Include(a => a.Mecanico).Where(a => a.MecanicoId == id);
        if (soloActivas) q = q.Where(a => a.Activa);
        var lista = await q.OrderByDescending(a => a.Inicio).Take(200).ToListAsync();
        return lista.Select(a => a.ToDto()).ToList();
    }

    /// <summary>Consulta previa: ¿el mecánico está libre en ese horario?</summary>
    [HttpGet("{id:int}/disponibilidad")]
    public async Task<ActionResult<DisponibilidadDto>> Disponibilidad(int id, [FromQuery] DateTime inicio, [FromQuery] DateTime fin) =>
        await _asignaciones.DisponibilidadAsync(id, inicio, fin);

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MecanicoDto>> Crear(MecanicoGuardarDto dto)
    {
        var email = Validacion.Correo(dto.Email);
        if (await _db.Mecanicos.AnyAsync(m => m.Email == email)) throw new ConflictException("Ya existe un mecánico con ese correo.");
        if (!string.IsNullOrWhiteSpace(dto.PasswordUsuario) && await _db.Usuarios.AnyAsync(u => u.Email == email))
            throw new ConflictException("Ya existe un usuario con ese correo.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var mecanico = new Mecanico
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Especialidad = dto.Especialidad.Trim(),
            Telefono = dto.Telefono.Trim(),
            Email = email
        };
        _db.Mecanicos.Add(mecanico);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(dto.PasswordUsuario))
        {
            var usuario = new Usuario
            {
                NombreCompleto = mecanico.NombreCompleto,
                Email = email,
                Rol = Roles.Mecanico,
                MecanicoId = mecanico.Id,
                Permisos = Permisos.PorDefecto(Roles.Mecanico).Select(p => new UsuarioPermiso { Permiso = p }).ToList()
            };
            usuario.PasswordHash = AuthService.Hashear(usuario, dto.PasswordUsuario);
            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return CreatedAtAction(nameof(Obtener), new { id = mecanico.Id }, (await ConvertirAsync(new List<Mecanico> { mecanico }))[0]);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MecanicoDto>> Actualizar(int id, MecanicoGuardarDto dto)
    {
        var m = await _db.Mecanicos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El mecánico no existe.");
        var email = Validacion.Correo(dto.Email);
        if (await _db.Mecanicos.AnyAsync(x => x.Email == email && x.Id != id)) throw new ConflictException("Ya existe otro mecánico con ese correo.");

        _db.ValidarVersion(m, dto.Version, "El mecánico");
        m.NombreCompleto = dto.NombreCompleto.Trim();
        m.Especialidad = dto.Especialidad.Trim();
        m.Telefono = dto.Telefono.Trim();
        m.Email = email;
        await _db.SaveChangesAsync();
        return (await ConvertirAsync(new List<Mecanico> { m }))[0];
    }

    [HttpPatch("{id:int}/estado")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MecanicoDto>> CambiarEstado(int id, CambiarEstadoActivoDto dto)
    {
        var m = await _db.Mecanicos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El mecánico no existe.");
        if (!dto.Activo && await _db.Asignaciones.AnyAsync(a => a.MecanicoId == id && a.Activa && a.Fin > DateTime.Now))
            throw new ConflictException("El mecánico tiene asignaciones pendientes; reasígnalas antes de desactivarlo.");
        m.Activo = dto.Activo;
        await _db.SaveChangesAsync();
        return (await ConvertirAsync(new List<Mecanico> { m }))[0];
    }

    private async Task<List<MecanicoDto>> ConvertirAsync(List<Mecanico> mecanicos)
    {
        var ids = mecanicos.Select(m => m.Id).ToList();
        var ahora = DateTime.Now;
        var activas = await _db.Asignaciones.AsNoTracking()
            .Where(a => ids.Contains(a.MecanicoId) && a.Activa && a.Fin > ahora)
            .GroupBy(a => a.MecanicoId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
        var conUsuario = await _db.Usuarios.AsNoTracking()
            .Where(u => u.MecanicoId != null && ids.Contains(u.MecanicoId.Value))
            .Select(u => u.MecanicoId!.Value).ToListAsync();

        return mecanicos.Select(m => new MecanicoDto(m.Id, m.NombreCompleto, m.Especialidad, m.Telefono, m.Email, m.Activo,
            m.UltimaAsignacion, m.Version, activas.GetValueOrDefault(m.Id), conUsuario.Contains(m.Id))).ToList();
    }
}
