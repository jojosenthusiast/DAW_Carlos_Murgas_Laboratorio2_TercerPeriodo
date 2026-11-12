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
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;

    public ReportesController(AutoFixDbContext db, IUsuarioActual usuario)
    {
        _db = db; _usuario = usuario;
    }

    /// <summary>Resumen del taller; se adapta al rol (el cliente solo ve lo suyo).</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard()
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var esCliente = _usuario.EsRol(Roles.Cliente);
        var esMecanico = _usuario.EsRol(Roles.Mecanico);
        var clienteId = _usuario.ClienteId ?? -1;
        var mecanicoId = _usuario.MecanicoId ?? -1;

        IQueryable<OrdenReparacion> ordenes = _db.Ordenes.AsNoTracking();
        if (esCliente) ordenes = ordenes.Where(o => o.ClienteId == clienteId);
        else if (esMecanico) ordenes = ordenes.Where(o => o.MecanicoId == mecanicoId);

        var porEstado = (await ordenes.GroupBy(o => o.Estado).Select(g => new { g.Key, Total = g.Count() }).ToListAsync())
            .Select(x => new ConteoEstadoDto(x.Key, x.Total)).OrderBy(x => x.Estado).ToList();
        var activas = porEstado.Where(x => x.Estado is not (EstadoOrden.Finalizado or EstadoOrden.Entregado or EstadoOrden.Cancelado)).Sum(x => x.Cantidad);
        var finalizadasMes = await ordenes.CountAsync(o => o.FechaFinalizacion != null && o.FechaFinalizacion >= inicioMes);

        var presupuestos = _db.Presupuestos.AsNoTracking();
        if (esCliente) presupuestos = presupuestos.Where(p => p.Orden!.ClienteId == clienteId);
        else if (esMecanico) presupuestos = presupuestos.Where(p => p.Orden!.MecanicoId == mecanicoId);
        var pendientes = await presupuestos.CountAsync(p => p.Estado == EstadoPresupuesto.PendienteAprobacion);
        // SQLite guarda decimal como texto: se suma en memoria.
        var montos = await presupuestos.Where(p => p.Estado == EstadoPresupuesto.Aprobado && p.FechaRespuesta >= inicioMes).Select(p => p.Total).ToListAsync();

        var totalClientes = esCliente ? 1 : await _db.Clientes.CountAsync();
        var totalVehiculos = esCliente ? await _db.Vehiculos.CountAsync(v => v.ClienteId == clienteId) : await _db.Vehiculos.CountAsync();
        var mecanicosActivos = esCliente ? 0 : await _db.Mecanicos.CountAsync(m => m.Activo);
        var manana = hoy.AddDays(1);
        var asignacionesQ = _db.Asignaciones.AsNoTracking().Where(a => a.Activa && a.Inicio < manana && a.Fin > hoy);
        if (esMecanico) asignacionesQ = asignacionesQ.Where(a => a.MecanicoId == mecanicoId);
        var asignacionesHoy = esCliente ? 0 : await asignacionesQ.CountAsync();

        var alertas = esCliente
            ? new List<RepuestoDto>()
            : (await _db.Repuestos.AsNoTracking().Where(r => r.Activo && r.Existencias <= r.StockMinimo).OrderBy(r => r.Existencias).Take(10).ToListAsync())
                .Select(r => r.ToDto()).ToList();

        var recientes = (await ordenes.Include(o => o.Cliente).Include(o => o.Vehiculo).Include(o => o.Mecanico)
                .OrderByDescending(o => o.FechaIngreso).Take(8).ToListAsync())
            .Select(o => o.ToResumen()).ToList();

        return new DashboardDto(_usuario.Rol, totalClientes, totalVehiculos, activas, finalizadasMes, pendientes,
            montos.Sum(), mecanicosActivos, asignacionesHoy, porEstado, alertas, recientes);
    }

    [HttpGet("ordenes")]
    [Authorize(Policy = Politicas.PuedeConsultarReportes)]
    public async Task<ActionResult<ReporteOrdenesDto>> Ordenes([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] EstadoOrden? estado)
    {
        var d = (desde ?? DateTime.Today.AddDays(-30)).Date;
        var h = (hasta ?? DateTime.Today).Date;
        if (h < d) throw new BusinessException("La fecha final no puede ser anterior a la inicial.");
        if ((h - d).TotalDays > 366) throw new BusinessException("El rango máximo del reporte es de un año.");
        var hFin = h.AddDays(1);

        var q = _db.Ordenes.AsNoTracking().Include(o => o.Cliente).Include(o => o.Vehiculo).Include(o => o.Mecanico).Include(o => o.Presupuesto)
            .Where(o => o.FechaIngreso >= d && o.FechaIngreso < hFin);
        if (estado.HasValue) { var e = estado.Value; q = q.Where(o => o.Estado == e); }
        var lista = await q.OrderByDescending(o => o.FechaIngreso).ToListAsync();

        var porEstado = lista.GroupBy(o => o.Estado).Select(g => new ConteoEstadoDto(g.Key, g.Count())).OrderBy(x => x.Estado).ToList();
        var porServicio = lista.GroupBy(o => o.TipoServicio).Select(g => new ConteoServicioDto(g.Key, g.Count())).OrderByDescending(x => x.Cantidad).ToList();
        var presupuestado = lista.Where(o => o.Presupuesto is not null).Sum(o => o.Presupuesto!.Total);
        var aprobado = lista.Where(o => o.Presupuesto?.Estado == EstadoPresupuesto.Aprobado).Sum(o => o.Presupuesto!.Total);

        return new ReporteOrdenesDto(d, h, lista.Count, presupuestado, aprobado, porEstado, porServicio, lista.Select(o => o.ToResumen()).ToList());
    }

    [HttpGet("inventario")]
    [Authorize(Policy = Politicas.PuedeConsultarReportes)]
    public async Task<ActionResult<ReporteInventarioDto>> Inventario([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var d = (desde ?? DateTime.Today.AddDays(-30)).Date;
        var hFin = (hasta ?? DateTime.Today).Date.AddDays(1);

        var repuestos = await _db.Repuestos.AsNoTracking().Where(r => r.Activo).OrderBy(r => r.Nombre).ToListAsync();
        var movimientos = _db.Movimientos.AsNoTracking().Where(m => m.Fecha >= d && m.Fecha < hFin);
        var entradas = await movimientos.Where(m => m.Tipo == TipoMovimiento.Entrada).SumAsync(m => (int?)m.Cantidad) ?? 0;
        var salidas = await movimientos.Where(m => m.Tipo == TipoMovimiento.Salida).SumAsync(m => (int?)m.Cantidad) ?? 0;

        var consumo = await movimientos.Where(m => m.Tipo == TipoMovimiento.Salida)
            .GroupBy(m => m.RepuestoId).Select(g => new { RepuestoId = g.Key, Total = g.Sum(x => x.Cantidad) })
            .OrderByDescending(x => x.Total).Take(10).ToListAsync();
        var idsConsumo = consumo.Select(c => c.RepuestoId).ToList();
        var nombres = await _db.Repuestos.AsNoTracking().Where(r => idsConsumo.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);

        return new ReporteInventarioDto(
            repuestos.Count,
            repuestos.Count(r => r.Existencias <= r.StockMinimo),
            repuestos.Sum(r => r.Precio * r.Existencias),
            entradas, salidas,
            repuestos.Select(r => r.ToDto()).ToList(),
            consumo.Where(c => nombres.ContainsKey(c.RepuestoId))
                   .Select(c => new ConsumoRepuestoDto(c.RepuestoId, nombres[c.RepuestoId].Codigo, nombres[c.RepuestoId].Nombre, c.Total)).ToList());
    }

    [HttpGet("mecanicos")]
    [Authorize(Policy = Politicas.PuedeConsultarReportes)]
    public async Task<ActionResult<List<CargaMecanicoDto>>> Mecanicos()
    {
        var ahora = DateTime.Now;
        var mecanicos = await _db.Mecanicos.AsNoTracking().OrderBy(m => m.NombreCompleto).ToListAsync();
        var ordenes = await _db.Ordenes.AsNoTracking().Where(o => o.MecanicoId != null)
            .Select(o => new { MecanicoId = o.MecanicoId!.Value, o.Estado }).ToListAsync();
        var futuras = await _db.Asignaciones.AsNoTracking().Where(a => a.Activa && a.Fin > ahora)
            .GroupBy(a => a.MecanicoId).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total);

        return mecanicos.Select(m => new CargaMecanicoDto(
            m.Id, m.NombreCompleto, m.Activo,
            ordenes.Count(o => o.MecanicoId == m.Id && o.Estado is not (EstadoOrden.Finalizado or EstadoOrden.Entregado or EstadoOrden.Cancelado)),
            ordenes.Count(o => o.MecanicoId == m.Id && o.Estado is (EstadoOrden.Finalizado or EstadoOrden.Entregado)),
            futuras.GetValueOrDefault(m.Id))).ToList();
    }
}

/// <summary>Consulta de la bitácora de solicitudes (logs con Correlation ID).</summary>
[ApiController]
[Route("api/logs")]
[Authorize(Policy = Politicas.PuedeConsultarReportes)]
public class LogsController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    public LogsController(AutoFixDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<LogDto>>> Listar([FromQuery] string? correlationId, [FromQuery] int? codigo,
        [FromQuery] bool soloErrores = false, [FromQuery] string? usuario = null, [FromQuery] int top = 200)
    {
        top = Math.Clamp(top, 1, 1000);
        var q = _db.LogsSolicitudes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(correlationId)) { var c = correlationId.Trim(); q = q.Where(l => l.CorrelationId == c); }
        if (codigo.HasValue) { var s = codigo.Value; q = q.Where(l => l.CodigoEstado == s); }
        if (soloErrores) q = q.Where(l => l.CodigoEstado >= 400);
        if (!string.IsNullOrWhiteSpace(usuario)) { var u = $"%{usuario.Trim()}%"; q = q.Where(l => EF.Functions.Like(l.Usuario, u)); }

        return await q.OrderByDescending(l => l.Id).Take(top)
            .Select(l => new LogDto(l.Id, l.Fecha, l.CorrelationId, l.Usuario, l.Metodo, l.Endpoint, l.CodigoEstado,
                l.DuracionMs, l.Ip, l.MensajeError, l.DetalleError))
            .ToListAsync();
    }
}
