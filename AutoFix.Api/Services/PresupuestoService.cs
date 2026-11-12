using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Services;

public class PresupuestoService
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly OrdenService _ordenes;
    private readonly InventarioService _inventario;
    private readonly ILogger<PresupuestoService> _logger;

    public PresupuestoService(AutoFixDbContext db, IUsuarioActual usuario, OrdenService ordenes,
        InventarioService inventario, ILogger<PresupuestoService> logger)
    {
        _db = db; _usuario = usuario; _ordenes = ordenes; _inventario = inventario; _logger = logger;
    }

    private IQueryable<Presupuesto> Consulta() => _db.Presupuestos
        .Include(p => p.Detalles).ThenInclude(d => d.Repuesto)
        .Include(p => p.Orden!).ThenInclude(o => o.Cliente)
        .Include(p => p.Orden!).ThenInclude(o => o.Vehiculo);

    public async Task<List<PresupuestoDto>> ListarAsync(EstadoPresupuesto? estado)
    {
        var q = Consulta().AsNoTracking().AsSplitQuery();
        if (_usuario.EsRol(Roles.Mecanico))
        {
            var propio = _usuario.MecanicoId ?? -1;
            q = q.Where(p => p.Orden!.MecanicoId == propio);
        }
        if (estado.HasValue) { var e = estado.Value; q = q.Where(p => p.Estado == e); }
        var lista = await q.OrderByDescending(p => p.FechaCreacion).Take(300).ToListAsync();
        return lista.Select(ToDto).ToList();
    }

    public async Task<PresupuestoDto> ObtenerAsync(int id)
    {
        var p = await Consulta().AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"El presupuesto {id} no existe.");
        _ordenes.VerificarAcceso(p.Orden!, edicion: false);
        return ToDto(p);
    }

    /// <summary>El cliente puede consultar el presupuesto de su orden y si fue aprobado o no.</summary>
    public async Task<PresupuestoDto> ObtenerPorOrdenAsync(int ordenId)
    {
        var orden = await _db.Ordenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException($"La orden {ordenId} no existe.");
        _ordenes.VerificarAcceso(orden, edicion: false);
        var p = await Consulta().AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.OrdenId == ordenId)
                ?? throw new NotFoundException("La orden aún no tiene presupuesto.");
        return ToDto(p);
    }

    public async Task<PresupuestoDto> CrearAsync(int ordenId, PresupuestoGuardarDto dto)
    {
        ValidarElaborador();
        await using var tx = await _db.Database.BeginTransactionAsync();

        var orden = await _db.Ordenes.Include(o => o.Presupuesto).FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException($"La orden {ordenId} no existe.");
        _ordenes.VerificarAcceso(orden, edicion: true);

        if (orden.Presupuesto is not null)
            throw new ConflictException("La orden ya tiene un presupuesto; modifícalo en lugar de crear otro.");
        if (orden.Estado != EstadoOrden.EnDiagnostico)
            throw new BusinessException("Solo se puede presupuestar una orden que está En diagnóstico.");
        if (string.IsNullOrWhiteSpace(orden.Diagnostico))
            throw new BusinessException("Registra el diagnóstico antes de crear el presupuesto.");

        var presupuesto = new Presupuesto { OrdenId = orden.Id, CreadoPor = _usuario.Email, Estado = EstadoPresupuesto.Borrador };
        await AplicarDatosAsync(presupuesto, dto);
        _db.Presupuestos.Add(presupuesto);

        var anterior = orden.Estado;
        orden.Estado = EstadoOrden.Presupuestado;
        orden.FechaActualizacion = DateTime.Now;
        orden.ActualizadoPor = _usuario.Email;
        _db.HistorialEstados.Add(new HistorialEstadoOrden
        {
            OrdenId = orden.Id, EstadoAnterior = anterior, EstadoNuevo = EstadoOrden.Presupuestado,
            Usuario = _usuario.Email, Comentario = $"Presupuesto elaborado por un total de ${presupuesto.Total:N2}."
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        _logger.LogInformation("Presupuesto creado para {Orden}: total {Total}", orden.NumeroOrden, presupuesto.Total);
        return await ObtenerAsync(presupuesto.Id);
    }

    public async Task<PresupuestoDto> ActualizarAsync(int id, PresupuestoGuardarDto dto)
    {
        ValidarElaborador();
        await using var tx = await _db.Database.BeginTransactionAsync();

        var p = await _db.Presupuestos.Include(x => x.Detalles).Include(x => x.Orden).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"El presupuesto {id} no existe.");
        _ordenes.VerificarAcceso(p.Orden!, edicion: true);

        if (p.Estado is not (EstadoPresupuesto.Borrador or EstadoPresupuesto.Rechazado))
            throw new BusinessException("Solo se puede modificar un presupuesto en Borrador o Rechazado.");

        _db.ValidarVersion(p, dto.Version, "El presupuesto");
        await AplicarDatosAsync(p, dto);
        p.Estado = EstadoPresupuesto.Borrador;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ObtenerAsync(id);
    }

    public async Task<PresupuestoDto> EnviarAprobacionAsync(int id, DecisionPresupuestoDto? dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var p = await _db.Presupuestos.Include(x => x.Detalles).Include(x => x.Orden).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"El presupuesto {id} no existe.");
        _ordenes.VerificarAcceso(p.Orden!, edicion: true);

        if (p.Estado != EstadoPresupuesto.Borrador)
            throw new BusinessException("Solo un presupuesto en Borrador puede enviarse a aprobación.");
        if (p.Orden!.Estado != EstadoOrden.Presupuestado)
            throw new BusinessException("La orden debe estar en estado Presupuestado.");

        _db.ValidarVersion(p, dto?.Version, "El presupuesto");
        p.Estado = EstadoPresupuesto.PendienteAprobacion;
        p.FechaEnvio = DateTime.Now;
        CambiarEstadoOrden(p.Orden, EstadoOrden.PendienteAprobacion, "Presupuesto enviado a aprobación.");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Aprobación (Supervisor con Ordenes.Aprobar): en UNA transacción descuenta todos los repuestos,
    /// registra movimientos y cambia estados. Si falta stock de cualquiera, no se descuenta nada.
    /// </summary>
    public async Task<PresupuestoDto> AprobarAsync(int id, DecisionPresupuestoDto? dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var p = await _db.Presupuestos.Include(x => x.Detalles).Include(x => x.Orden).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"El presupuesto {id} no existe.");

        if (p.Estado != EstadoPresupuesto.PendienteAprobacion)
            throw new BusinessException($"Solo se aprueban presupuestos pendientes de aprobación (estado actual: {p.Estado}).");
        _db.ValidarVersion(p, dto?.Version, "El presupuesto");

        var ids = p.Detalles.Select(d => d.RepuestoId).ToList();
        var repuestos = await _db.Repuestos.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        var faltantes = p.Detalles
            .Where(d => repuestos[d.RepuestoId].Existencias < d.Cantidad)
            .Select(d => $"{repuestos[d.RepuestoId].Codigo} (requiere {d.Cantidad}, hay {repuestos[d.RepuestoId].Existencias})")
            .ToList();
        if (faltantes.Count > 0)
            throw new BusinessException("No se puede aprobar: existencias insuficientes de " + string.Join("; ", faltantes) +
                                        ". Registra una entrada de inventario o modifica el presupuesto.");

        foreach (var d in p.Detalles)
            _inventario.AplicarSalida(repuestos[d.RepuestoId], d.Cantidad, $"Presupuesto aprobado - orden {p.Orden!.NumeroOrden}", p.OrdenId);

        p.InventarioDescontado = p.Detalles.Count > 0;
        p.Estado = EstadoPresupuesto.Aprobado;
        p.FechaRespuesta = DateTime.Now;
        p.RespondidoPor = _usuario.Email;
        CambiarEstadoOrden(p.Orden!, EstadoOrden.Aprobado,
            "Presupuesto aprobado." + (string.IsNullOrWhiteSpace(dto?.Comentario) ? "" : $" {dto!.Comentario!.Trim()}"));

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            throw new ConflictException("Otro usuario modificó el presupuesto o las existencias de un repuesto al mismo tiempo. Intenta de nuevo.");
        }

        _logger.LogInformation("Presupuesto {Id} aprobado por {Usuario}", id, _usuario.Email);
        return await ObtenerAsync(id);
    }

    public async Task<PresupuestoDto> RechazarAsync(int id, DecisionPresupuestoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Comentario) || dto.Comentario.Trim().Length < 5)
            throw new BusinessException("Indica el motivo del rechazo (mínimo 5 caracteres).");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var p = await _db.Presupuestos.Include(x => x.Orden).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"El presupuesto {id} no existe.");
        if (p.Estado != EstadoPresupuesto.PendienteAprobacion)
            throw new BusinessException("Solo se rechazan presupuestos pendientes de aprobación.");

        _db.ValidarVersion(p, dto.Version, "El presupuesto");
        p.Estado = EstadoPresupuesto.Rechazado;
        p.FechaRespuesta = DateTime.Now;
        p.RespondidoPor = _usuario.Email;
        p.MotivoRechazo = dto.Comentario.Trim();
        CambiarEstadoOrden(p.Orden!, EstadoOrden.Presupuestado, $"Presupuesto rechazado: {p.MotivoRechazo}");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ObtenerAsync(id);
    }

    // ---------------- Auxiliares ----------------

    private void ValidarElaborador()
    {
        if (!_usuario.EsRol(Roles.Mecanico, Roles.Supervisor, Roles.Administrador))
            throw new ForbiddenException("Solo el mecánico asignado, un supervisor o el administrador elaboran presupuestos.");
    }

    /// <summary>Precios tomados del inventario (nunca del cliente); subtotales y total calculados en el backend.</summary>
    private async Task AplicarDatosAsync(Presupuesto p, PresupuestoGuardarDto dto)
    {
        var lineas = (dto.Detalles ?? new List<DetalleGuardarDto>())
            .Where(d => d.RepuestoId > 0)
            .GroupBy(d => d.RepuestoId)
            .Select(g => new { RepuestoId = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToList();

        if (lineas.Any(l => l.Cantidad <= 0 || l.Cantidad > 1000))
            throw new BusinessException("Cada cantidad debe estar entre 1 y 1000.");

        var ids = lineas.Select(l => l.RepuestoId).ToList();
        var repuestos = await _db.Repuestos.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        var inexistentes = ids.Where(i => !repuestos.ContainsKey(i)).ToList();
        if (inexistentes.Count > 0)
            throw new NotFoundException($"No existen los repuestos con Id: {string.Join(", ", inexistentes)}.");
        var inactivos = repuestos.Values.Where(r => !r.Activo).Select(r => r.Codigo).ToList();
        if (inactivos.Count > 0)
            throw new BusinessException($"Los repuestos {string.Join(", ", inactivos)} están inactivos.");

        if (p.Detalles.Count > 0)
        {
            _db.DetallesPresupuesto.RemoveRange(p.Detalles);
            p.Detalles.Clear();
        }

        foreach (var l in lineas)
        {
            var r = repuestos[l.RepuestoId];
            p.Detalles.Add(new DetallePresupuesto
            {
                RepuestoId = r.Id,
                Repuesto = r,
                Cantidad = l.Cantidad,
                PrecioUnitario = r.Precio,
                Subtotal = Math.Round(r.Precio * l.Cantidad, 2)
            });
        }

        p.ManoObra = Math.Round(dto.ManoObra, 2);
        p.TotalRepuestos = p.Detalles.Sum(d => d.Subtotal);
        p.Total = p.ManoObra + p.TotalRepuestos;
        p.Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim();

        if (p.Total <= 0)
            throw new BusinessException("El presupuesto debe incluir mano de obra o al menos un repuesto.");
    }

    private void CambiarEstadoOrden(OrdenReparacion orden, EstadoOrden nuevo, string comentario)
    {
        var anterior = orden.Estado;
        orden.Estado = nuevo;
        orden.FechaActualizacion = DateTime.Now;
        orden.ActualizadoPor = _usuario.Email;
        _db.HistorialEstados.Add(new HistorialEstadoOrden
        {
            OrdenId = orden.Id, EstadoAnterior = anterior, EstadoNuevo = nuevo,
            Usuario = _usuario.Email, Comentario = Validacion.Recortar(comentario, 500)
        });
    }

    private static PresupuestoDto ToDto(Presupuesto p)
    {
        var advertencias = new List<string>();
        if (p.Estado != EstadoPresupuesto.Aprobado)
        {
            foreach (var d in p.Detalles.Where(d => d.Repuesto is not null && d.Cantidad > d.Repuesto.Existencias))
                advertencias.Add($"Stock insuficiente de {d.Repuesto!.Codigo}: requiere {d.Cantidad}, hay {d.Repuesto.Existencias}.");
        }

        return new PresupuestoDto(
            p.Id, p.OrdenId, p.Orden?.NumeroOrden ?? "", p.Orden?.Cliente?.NombreCompleto ?? "",
            p.Orden?.Vehiculo?.Placa ?? "", p.Orden?.Estado ?? EstadoOrden.Recibido,
            p.ManoObra, p.TotalRepuestos, p.Total, p.Observaciones,
            p.Estado, p.Estado == EstadoPresupuesto.Aprobado,
            p.FechaCreacion, p.CreadoPor, p.FechaEnvio, p.FechaRespuesta, p.RespondidoPor, p.MotivoRechazo,
            p.InventarioDescontado, p.Version,
            p.Detalles.Select(d => new DetallePresupuestoDto(
                d.Id, d.RepuestoId, d.Repuesto?.Codigo ?? "", d.Repuesto?.Nombre ?? "",
                d.Cantidad, d.PrecioUnitario, d.Subtotal, d.Repuesto?.Existencias ?? 0)).ToList(),
            advertencias);
    }
}
