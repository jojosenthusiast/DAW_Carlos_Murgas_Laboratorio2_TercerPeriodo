using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Services;

public class AsignacionService
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly ILogger<AsignacionService> _logger;

    public AsignacionService(AutoFixDbContext db, IUsuarioActual usuario, ILogger<AsignacionService> logger)
    {
        _db = db; _usuario = usuario; _logger = logger;
    }

    /// <summary>
    /// Escenario B: dos supervisores asignan al mismo mecánico en horarios que se cruzan.
    /// 1) Transacción. 2) Validación de cruce: nuevoInicio &lt; finExistente y inicioExistente &lt; nuevoFin.
    /// 3) Se modifica la fila del mecánico (token Version) para que, si dos asignan a la vez,
    ///    uno reciba DbUpdateConcurrencyException -> 409.
    /// </summary>
    public async Task<AsignacionDto> AsignarAsync(int ordenId, AsignarMecanicoDto dto)
    {
        var inicio = Validacion.ALocal(dto.Inicio!.Value);
        var fin = Validacion.ALocal(dto.Fin!.Value);
        if (fin <= inicio) throw new BusinessException("El fin debe ser posterior al inicio.");
        if ((fin - inicio).TotalHours > 12) throw new BusinessException("Una asignación no puede durar más de 12 horas.");
        if (fin <= DateTime.Now) throw new BusinessException("El horario de la asignación ya pasó.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var orden = await _db.Ordenes.Include(o => o.Asignaciones).FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException($"La orden {ordenId} no existe.");
        if (orden.Estado is EstadoOrden.Finalizado or EstadoOrden.Entregado or EstadoOrden.Cancelado)
            throw new BusinessException($"No se puede asignar un mecánico a una orden en estado {orden.Estado}.");
        _db.ValidarVersion(orden, dto.VersionOrden, "La orden");

        var mecanico = await _db.Mecanicos.FirstOrDefaultAsync(m => m.Id == dto.MecanicoId)
                       ?? throw new NotFoundException("El mecánico no existe.");
        if (!mecanico.Activo) throw new BusinessException($"{mecanico.NombreCompleto} está inactivo y no puede recibir asignaciones.");
        _db.ValidarVersion(mecanico, dto.VersionMecanico, "El mecánico");

        var conflicto = await _db.Asignaciones.AsNoTracking().Include(a => a.Orden)
            .Where(a => a.MecanicoId == mecanico.Id && a.Activa && a.OrdenId != ordenId
                        && inicio < a.Fin && a.Inicio < fin)
            .OrderBy(a => a.Inicio)
            .FirstOrDefaultAsync();
        if (conflicto is not null)
            throw new ConflictException(
                $"{mecanico.NombreCompleto} ya tiene asignada la orden {conflicto.Orden?.NumeroOrden} " +
                $"de {conflicto.Inicio:dd/MM/yyyy HH:mm} a {conflicto.Fin:dd/MM/yyyy HH:mm}; el horario se cruza con el solicitado.");

        // Reasignación: la asignación anterior de esta orden queda inactiva.
        foreach (var previa in orden.Asignaciones.Where(a => a.Activa)) previa.Activa = false;

        var asignacion = new AsignacionMecanico
        {
            MecanicoId = mecanico.Id,
            OrdenId = orden.Id,
            Inicio = inicio,
            Fin = fin,
            AsignadoPor = _usuario.Email
        };
        _db.Asignaciones.Add(asignacion);

        orden.MecanicoId = mecanico.Id;
        orden.FechaActualizacion = DateTime.Now;
        orden.ActualizadoPor = _usuario.Email;
        mecanico.UltimaAsignacion = DateTime.Now; // modifica la fila -> cambia su token Version

        _db.HistorialEstados.Add(new HistorialEstadoOrden
        {
            OrdenId = orden.Id,
            EstadoAnterior = orden.Estado,
            EstadoNuevo = orden.Estado,
            Usuario = _usuario.Email,
            Comentario = $"Mecánico asignado: {mecanico.NombreCompleto} ({inicio:dd/MM HH:mm} - {fin:dd/MM HH:mm})."
        });

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            _logger.LogWarning("Conflicto de concurrencia al asignar el mecánico {MecanicoId} a la orden {OrdenId}", mecanico.Id, ordenId);
            throw new ConflictException("Otro supervisor asignó a este mecánico (o modificó la orden) al mismo tiempo. Recarga e intenta de nuevo.");
        }

        _logger.LogInformation("Mecánico {Mecanico} asignado a {Orden} por {Usuario}", mecanico.NombreCompleto, orden.NumeroOrden, _usuario.Email);
        asignacion.Mecanico = mecanico;
        return asignacion.ToDto(orden.NumeroOrden);
    }

    public async Task<DisponibilidadDto> DisponibilidadAsync(int mecanicoId, DateTime inicio, DateTime fin)
    {
        inicio = Validacion.ALocal(inicio);
        fin = Validacion.ALocal(fin);
        if (fin <= inicio) throw new BusinessException("El fin debe ser posterior al inicio.");
        var mecanico = await _db.Mecanicos.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mecanicoId)
                       ?? throw new NotFoundException("El mecánico no existe.");
        var conflictos = await _db.Asignaciones.AsNoTracking().Include(a => a.Orden).Include(a => a.Mecanico)
            .Where(a => a.MecanicoId == mecanicoId && a.Activa && inicio < a.Fin && a.Inicio < fin)
            .OrderBy(a => a.Inicio).ToListAsync();
        return new DisponibilidadDto(mecanico.Id, mecanico.NombreCompleto, mecanico.Activo && conflictos.Count == 0,
            conflictos.Select(a => a.ToDto()).ToList());
    }
}
