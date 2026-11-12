using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Services;

public class InventarioService
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly ILogger<InventarioService> _logger;

    public InventarioService(AutoFixDbContext db, IUsuarioActual usuario, ILogger<InventarioService> logger)
    {
        _db = db; _usuario = usuario; _logger = logger;
    }

    /// <summary>Descuenta existencias y registra el movimiento (sin guardar; el llamador maneja la transacción).</summary>
    public MovimientoInventario AplicarSalida(Repuesto repuesto, int cantidad, string motivo, int? ordenId)
    {
        if (cantidad <= 0) throw new BusinessException("La cantidad debe ser mayor que cero.");
        if (!repuesto.Activo) throw new BusinessException($"El repuesto {repuesto.Codigo} está inactivo.");
        if (repuesto.Existencias < cantidad)
            throw new BusinessException($"Existencias insuficientes de {repuesto.Codigo} - {repuesto.Nombre}: disponibles {repuesto.Existencias}, solicitadas {cantidad}.");

        var movimiento = new MovimientoInventario
        {
            RepuestoId = repuesto.Id,
            Repuesto = repuesto,
            Tipo = TipoMovimiento.Salida,
            Cantidad = cantidad,
            ExistenciaAnterior = repuesto.Existencias,
            ExistenciaNueva = repuesto.Existencias - cantidad,
            Usuario = _usuario.Email,
            Motivo = Validacion.Recortar(motivo, 250),
            OrdenId = ordenId,
            Fecha = DateTime.Now
        };
        repuesto.Existencias -= cantidad;
        _db.Movimientos.Add(movimiento);
        return movimiento;
    }

    public MovimientoInventario AplicarEntrada(Repuesto repuesto, int cantidad, string motivo, int? ordenId)
    {
        if (cantidad <= 0) throw new BusinessException("La cantidad debe ser mayor que cero.");
        var movimiento = new MovimientoInventario
        {
            RepuestoId = repuesto.Id,
            Repuesto = repuesto,
            Tipo = TipoMovimiento.Entrada,
            Cantidad = cantidad,
            ExistenciaAnterior = repuesto.Existencias,
            ExistenciaNueva = repuesto.Existencias + cantidad,
            Usuario = _usuario.Email,
            Motivo = Validacion.Recortar(motivo, 250),
            OrdenId = ordenId,
            Fecha = DateTime.Now
        };
        repuesto.Existencias += cantidad;
        _db.Movimientos.Add(movimiento);
        return movimiento;
    }

    /// <summary>
    /// Escenario A: descontar inventario con transacción + token de concurrencia.
    /// Si dos usuarios intentan usar las mismas unidades a la vez, solo uno lo logra;
    /// el otro recibe 409 (conflicto) o 400 (existencias insuficientes). Nunca queda negativo.
    /// </summary>
    public async Task<MovimientoResultadoDto> RegistrarSalidaAsync(int repuestoId, int cantidad, string motivo, int? ordenId, Guid? versionEsperada)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var repuesto = await _db.Repuestos.FirstOrDefaultAsync(r => r.Id == repuestoId)
                       ?? throw new NotFoundException("El repuesto no existe.");
        if (ordenId.HasValue && !await _db.Ordenes.AnyAsync(o => o.Id == ordenId.Value))
            throw new NotFoundException("La orden indicada no existe.");

        _db.ValidarVersion(repuesto, versionEsperada, "El repuesto");
        var movimiento = AplicarSalida(repuesto, cantidad, motivo, ordenId);

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            _logger.LogWarning("Conflicto de concurrencia al descontar el repuesto {RepuestoId}", repuestoId);
            throw new ConflictException("Otro usuario modificó el repuesto. Intenta de nuevo.");
        }

        return Resultado(movimiento, repuesto);
    }

    public async Task<MovimientoResultadoDto> RegistrarEntradaAsync(int repuestoId, int cantidad, string motivo, Guid? versionEsperada)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var repuesto = await _db.Repuestos.FirstOrDefaultAsync(r => r.Id == repuestoId)
                       ?? throw new NotFoundException("El repuesto no existe.");
        if (!repuesto.Activo) throw new BusinessException("No se pueden registrar entradas de un repuesto inactivo.");

        _db.ValidarVersion(repuesto, versionEsperada, "El repuesto");
        var movimiento = AplicarEntrada(repuesto, cantidad, motivo, null);

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            throw new ConflictException("Otro usuario modificó el repuesto. Intenta de nuevo.");
        }

        return Resultado(movimiento, repuesto);
    }

    private MovimientoResultadoDto Resultado(MovimientoInventario movimiento, Repuesto repuesto)
    {
        var alerta = repuesto.Existencias <= repuesto.StockMinimo;
        string? mensaje = null;
        if (alerta)
        {
            mensaje = $"Alerta: {repuesto.Codigo} - {repuesto.Nombre} llegó al stock mínimo ({repuesto.Existencias} de mínimo {repuesto.StockMinimo}).";
            _logger.LogWarning("{Mensaje}", mensaje);
        }
        return new MovimientoResultadoDto(movimiento.ToDto(), repuesto.ToDto(), alerta, mensaje);
    }
}
