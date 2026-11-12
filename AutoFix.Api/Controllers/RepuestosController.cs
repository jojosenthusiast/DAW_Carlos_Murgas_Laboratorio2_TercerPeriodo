using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Controllers;

/// <summary>Inventario de repuestos. Consultar: personal. Modificar precios/existencias: Inventario.Gestionar.</summary>
[ApiController]
[Route("api/repuestos")]
[Authorize(Policy = Politicas.PersonalTaller)]
public class RepuestosController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly InventarioService _inventario;
    private readonly ImagenService _imagenes;
    private readonly IUsuarioActual _usuario;

    public RepuestosController(AutoFixDbContext db, InventarioService inventario, ImagenService imagenes, IUsuarioActual usuario)
    {
        _db = db; _inventario = inventario; _imagenes = imagenes; _usuario = usuario;
    }

    [HttpGet]
    public async Task<ActionResult<List<RepuestoDto>>> Listar([FromQuery] string? buscar, [FromQuery] string? categoria,
        [FromQuery] bool soloAlertas = false, [FromQuery] bool incluirInactivos = false)
    {
        var q = _db.Repuestos.AsNoTracking();
        if (!incluirInactivos) q = q.Where(r => r.Activo);
        if (soloAlertas) q = q.Where(r => r.Existencias <= r.StockMinimo);
        if (!string.IsNullOrWhiteSpace(categoria)) { var c = categoria.Trim(); q = q.Where(r => r.Categoria == c); }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = $"%{buscar.Trim()}%";
            q = q.Where(r => EF.Functions.Like(r.Codigo, b) || EF.Functions.Like(r.Nombre, b) || EF.Functions.Like(r.Marca, b));
        }
        var lista = await q.OrderBy(r => r.Nombre).ToListAsync();
        return lista.Select(r => r.ToDto()).ToList();
    }

    /// <summary>Repuestos que llegaron al stock mínimo.</summary>
    [HttpGet("alertas")]
    public async Task<ActionResult<List<RepuestoDto>>> Alertas()
    {
        var lista = await _db.Repuestos.AsNoTracking().Where(r => r.Activo && r.Existencias <= r.StockMinimo)
            .OrderBy(r => r.Existencias).ToListAsync();
        return lista.Select(r => r.ToDto()).ToList();
    }

    [HttpGet("categorias")]
    public async Task<ActionResult<List<string>>> Categorias() =>
        await _db.Repuestos.AsNoTracking().Select(r => r.Categoria).Distinct().OrderBy(c => c).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RepuestoDto>> Obtener(int id)
    {
        var r = await _db.Repuestos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El repuesto no existe.");
        return r.ToDto();
    }

    [HttpPost]
    [Authorize(Policy = Politicas.PuedeGestionarInventario)]
    public async Task<ActionResult<RepuestoDto>> Crear(RepuestoGuardarDto dto)
    {
        var codigo = Validacion.Normalizar(dto.Codigo);
        if (await _db.Repuestos.AnyAsync(r => r.Codigo == codigo)) throw new ConflictException($"Ya existe un repuesto con el código {codigo}.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var repuesto = new Repuesto
        {
            Codigo = codigo,
            Nombre = dto.Nombre.Trim(),
            Descripcion = dto.Descripcion?.Trim() ?? "",
            Categoria = dto.Categoria.Trim(),
            Marca = dto.Marca.Trim(),
            Precio = Math.Round(dto.Precio, 2),
            Existencias = 0,
            StockMinimo = dto.StockMinimo
        };
        _db.Repuestos.Add(repuesto);
        await _db.SaveChangesAsync();

        if (dto.Existencias > 0)
        {
            // La existencia inicial también queda registrada como movimiento de entrada.
            _inventario.AplicarEntrada(repuesto, dto.Existencias, "Existencia inicial", null);
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return CreatedAtAction(nameof(Obtener), new { id = repuesto.Id }, repuesto.ToDto());
    }

    /// <summary>Modifica datos y precio. Las existencias solo cambian con entradas/salidas.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.PuedeGestionarInventario)]
    public async Task<ActionResult<RepuestoDto>> Actualizar(int id, RepuestoGuardarDto dto)
    {
        var r = await _db.Repuestos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El repuesto no existe.");
        var codigo = Validacion.Normalizar(dto.Codigo);
        if (await _db.Repuestos.AnyAsync(x => x.Codigo == codigo && x.Id != id)) throw new ConflictException($"Ya existe otro repuesto con el código {codigo}.");

        _db.ValidarVersion(r, dto.Version, "El repuesto");
        r.Codigo = codigo;
        r.Nombre = dto.Nombre.Trim();
        r.Descripcion = dto.Descripcion?.Trim() ?? "";
        r.Categoria = dto.Categoria.Trim();
        r.Marca = dto.Marca.Trim();
        r.Precio = Math.Round(dto.Precio, 2);
        r.StockMinimo = dto.StockMinimo;
        await _db.SaveChangesAsync();
        return r.ToDto();
    }

    /// <summary>Activa o desactiva (no se borra para conservar el historial de movimientos).</summary>
    [HttpPatch("{id:int}/estado")]
    [Authorize(Policy = Politicas.PuedeGestionarInventario)]
    public async Task<ActionResult<RepuestoDto>> CambiarEstado(int id, CambiarEstadoActivoDto dto)
    {
        var r = await _db.Repuestos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El repuesto no existe.");
        if (!dto.Activo && await _db.DetallesPresupuesto.AnyAsync(d => d.RepuestoId == id
                && (d.Presupuesto!.Estado == EstadoPresupuesto.Borrador || d.Presupuesto.Estado == EstadoPresupuesto.PendienteAprobacion)))
            throw new ConflictException("El repuesto está en presupuestos pendientes; no puede desactivarse.");
        r.Activo = dto.Activo;
        await _db.SaveChangesAsync();
        return r.ToDto();
    }

    [HttpPost("{id:int}/foto")]
    [Authorize(Policy = Politicas.PuedeGestionarInventario)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<RepuestoDto>> SubirFoto(int id, [FromForm] SubirFotoDto dto)
    {
        var r = await _db.Repuestos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El repuesto no existe.");
        var archivo = await _imagenes.GuardarAsync(dto.Archivo, "repuestos");
        var anterior = r.FotoRuta;
        r.FotoRuta = archivo.Ruta;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _imagenes.Eliminar(archivo.Ruta);
            throw;
        }
        _imagenes.Eliminar(anterior);
        return r.ToDto();
    }

    [HttpPost("{id:int}/entradas")]
    [Authorize(Policy = Politicas.PuedeGestionarInventario)]
    public async Task<ActionResult<MovimientoResultadoDto>> Entrada(int id, MovimientoCrearDto dto) =>
        await _inventario.RegistrarEntradaAsync(id, dto.Cantidad, dto.Motivo.Trim(), dto.Version);

    /// <summary>Escenario A: salida con transacción y token de concurrencia (nunca negativo).</summary>
    [HttpPost("{id:int}/salidas")]
    [Authorize(Policy = Politicas.PuedeGestionarInventario)]
    public async Task<ActionResult<MovimientoResultadoDto>> Salida(int id, MovimientoCrearDto dto) =>
        await _inventario.RegistrarSalidaAsync(id, dto.Cantidad, dto.Motivo.Trim(), dto.OrdenId, dto.Version);

    [HttpGet("{id:int}/movimientos")]
    public async Task<ActionResult<List<MovimientoDto>>> Movimientos(int id)
    {
        if (!await _db.Repuestos.AnyAsync(r => r.Id == id)) throw new NotFoundException("El repuesto no existe.");
        var lista = await _db.Movimientos.AsNoTracking().Include(m => m.Repuesto).Include(m => m.Orden)
            .Where(m => m.RepuestoId == id).OrderByDescending(m => m.Fecha).Take(200).ToListAsync();
        return lista.Select(m => m.ToDto()).ToList();
    }
}

[ApiController]
[Route("api/movimientos")]
[Authorize(Policy = Politicas.PersonalTaller)]
public class MovimientosController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    public MovimientosController(AutoFixDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<MovimientoDto>>> Listar([FromQuery] int? repuestoId, [FromQuery] TipoMovimiento? tipo,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int? ordenId)
    {
        var q = _db.Movimientos.AsNoTracking().Include(m => m.Repuesto).Include(m => m.Orden).AsQueryable();
        if (repuestoId.HasValue) { var r = repuestoId.Value; q = q.Where(m => m.RepuestoId == r); }
        if (tipo.HasValue) { var t = tipo.Value; q = q.Where(m => m.Tipo == t); }
        if (ordenId.HasValue) { var o = ordenId.Value; q = q.Where(m => m.OrdenId == o); }
        if (desde.HasValue) { var d = desde.Value.Date; q = q.Where(m => m.Fecha >= d); }
        if (hasta.HasValue) { var h = hasta.Value.Date.AddDays(1); q = q.Where(m => m.Fecha < h); }
        var lista = await q.OrderByDescending(m => m.Fecha).Take(500).ToListAsync();
        return lista.Select(m => m.ToDto()).ToList();
    }
}
