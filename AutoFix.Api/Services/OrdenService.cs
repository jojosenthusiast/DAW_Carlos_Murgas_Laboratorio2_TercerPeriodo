using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFix.Api.Services;

public class OrdenService
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly InventarioService _inventario;
    private readonly ILogger<OrdenService> _logger;

    public OrdenService(AutoFixDbContext db, IUsuarioActual usuario, InventarioService inventario, ILogger<OrdenService> logger)
    {
        _db = db; _usuario = usuario; _inventario = inventario; _logger = logger;
    }

    private IQueryable<OrdenReparacion> ConsultaBase() =>
        _db.Ordenes.Include(o => o.Cliente).Include(o => o.Vehiculo).Include(o => o.Mecanico);

    /// <summary>Cliente: solo sus órdenes. Mecánico: solo las asignadas. Personal: todas.</summary>
    public void VerificarAcceso(OrdenReparacion orden, bool edicion)
    {
        if (_usuario.EsRol(Roles.Cliente))
        {
            if (edicion || orden.ClienteId != _usuario.ClienteId)
                throw new ForbiddenException("No tienes permiso para acceder a esta orden.");
        }
        else if (_usuario.EsRol(Roles.Mecanico))
        {
            if (orden.MecanicoId is null || orden.MecanicoId != _usuario.MecanicoId)
                throw new ForbiddenException("Solo puedes consultar o actualizar las órdenes que tienes asignadas.");
        }
    }

    public async Task<List<OrdenResumenDto>> ListarAsync(EstadoOrden? estado, string? buscar, int? mecanicoId, int? clienteId)
    {
        var q = ConsultaBase().AsNoTracking();

        if (_usuario.EsRol(Roles.Cliente))
        {
            var propio = _usuario.ClienteId ?? -1;
            q = q.Where(o => o.ClienteId == propio);
        }
        else if (_usuario.EsRol(Roles.Mecanico))
        {
            var propio = _usuario.MecanicoId ?? -1;
            q = q.Where(o => o.MecanicoId == propio);
        }
        else
        {
            if (mecanicoId.HasValue) { var m = mecanicoId.Value; q = q.Where(o => o.MecanicoId == m); }
            if (clienteId.HasValue) { var c = clienteId.Value; q = q.Where(o => o.ClienteId == c); }
        }

        if (estado.HasValue) { var e = estado.Value; q = q.Where(o => o.Estado == e); }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = $"%{buscar.Trim()}%";
            q = q.Where(o => EF.Functions.Like(o.NumeroOrden, b)
                          || EF.Functions.Like(o.Vehiculo!.Placa, b)
                          || EF.Functions.Like(o.Cliente!.NombreCompleto, b));
        }

        var lista = await q.OrderByDescending(o => o.FechaIngreso).Take(500).ToListAsync();
        return lista.Select(o => o.ToResumen()).ToList();
    }

    public async Task<OrdenDetalleDto> ObtenerAsync(int id)
    {
        var orden = await ConsultaBase().AsNoTracking().AsSplitQuery()
            .Include(o => o.Presupuesto)
            .Include(o => o.Historial)
            .Include(o => o.Evidencias)
            .Include(o => o.Asignaciones).ThenInclude(a => a.Mecanico)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"La orden {id} no existe.");

        VerificarAcceso(orden, edicion: false);
        return ConstruirDetalle(orden);
    }

    private OrdenDetalleDto ConstruirDetalle(OrdenReparacion o)
    {
        var transiciones = _usuario.EsRol(Roles.Cliente)
            ? new List<EstadoOrden>()
            : ReglasEstadoOrden.Manuales.Where(e => Evaluar(o, e).Permitido).ToList();

        return new OrdenDetalleDto(
            o.Id, o.NumeroOrden,
            o.ClienteId, o.Cliente?.NombreCompleto ?? "", o.Cliente?.Telefono ?? "",
            o.VehiculoId, o.Vehiculo?.Placa ?? "",
            o.Vehiculo is null ? "" : $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Anio} ({o.Vehiculo.Color})",
            o.Vehiculo?.FotoRuta,
            o.TipoServicio, o.Estado, o.FechaIngreso, o.FechaEstimadaFinalizacion,
            o.DescripcionProblema, o.Diagnostico,
            o.MecanicoId, o.Mecanico?.NombreCompleto,
            o.FechaFinalizacion, o.FechaEntrega, o.MotivoCancelacion,
            o.Version,
            o.Presupuesto?.Id, o.Presupuesto?.Estado, o.Presupuesto?.Total,
            o.Historial.OrderBy(h => h.Fecha).Select(h => h.ToDto()).ToList(),
            o.Evidencias.OrderByDescending(e => e.FechaSubida).Select(e => e.ToDto(o.NumeroOrden)).ToList(),
            o.Asignaciones.OrderByDescending(a => a.Inicio).Select(a => a.ToDto(o.NumeroOrden)).ToList(),
            transiciones);
    }

    public async Task<OrdenDetalleDto> CrearAsync(CrearOrdenDto dto)
    {
        var ingreso = Validacion.ALocal(dto.FechaIngreso ?? DateTime.Now);
        var estimada = Validacion.ALocal(dto.FechaEstimadaFinalizacion!.Value);
        var tipo = dto.TipoServicio!.Value;

        if (ingreso > DateTime.Now.AddHours(1))
            throw new BusinessException("La fecha de ingreso no puede estar en el futuro.");
        if (ingreso < DateTime.Now.AddDays(-7))
            throw new BusinessException("La fecha de ingreso no puede ser de hace más de 7 días.");
        if (estimada <= ingreso)
            throw new BusinessException("La fecha estimada debe ser posterior a la fecha de ingreso.");
        if (estimada > ingreso.AddYears(1))
            throw new BusinessException("La fecha estimada no puede superar un año desde el ingreso.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var vehiculo = await _db.Vehiculos.FirstOrDefaultAsync(v => v.Id == dto.VehiculoId)
                       ?? throw new NotFoundException("El vehículo no existe.");
        if (vehiculo.ClienteId != dto.ClienteId)
            throw new BusinessException("El vehículo seleccionado no pertenece al cliente indicado.");

        // Problema 1: evitar órdenes duplicadas (mismo vehículo + mismo servicio, orden activa).
        var duplicada = await _db.Ordenes.AsNoTracking()
            .Where(o => o.VehiculoId == vehiculo.Id && o.TipoServicio == tipo
                        && o.Estado != EstadoOrden.Entregado && o.Estado != EstadoOrden.Cancelado)
            .Select(o => o.NumeroOrden)
            .FirstOrDefaultAsync();
        if (duplicada is not null)
            throw new ConflictException($"Ya existe la orden activa {duplicada} para el vehículo {vehiculo.Placa} con el servicio {tipo}. No se permiten órdenes duplicadas.");

        var orden = new OrdenReparacion
        {
            NumeroOrden = $"TMP-{Guid.NewGuid():N}",
            ClienteId = vehiculo.ClienteId,
            VehiculoId = vehiculo.Id,
            TipoServicio = tipo,
            FechaIngreso = ingreso,
            FechaEstimadaFinalizacion = estimada,
            DescripcionProblema = dto.DescripcionProblema.Trim(),
            Estado = EstadoOrden.Recibido,
            CreadoPor = _usuario.Email
        };
        orden.Historial.Add(new HistorialEstadoOrden
        {
            EstadoAnterior = null,
            EstadoNuevo = EstadoOrden.Recibido,
            Usuario = _usuario.Email,
            Comentario = "Orden registrada en recepción."
        });
        _db.Ordenes.Add(orden);
        await _db.SaveChangesAsync();

        orden.NumeroOrden = $"OT-{ingreso:yyyy}-{orden.Id:D5}";
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation("Orden {Numero} creada por {Usuario}", orden.NumeroOrden, _usuario.Email);
        return await ObtenerAsync(orden.Id);
    }

    public async Task<OrdenDetalleDto> EditarAsync(int id, EditarOrdenDto dto)
    {
        var orden = await _db.Ordenes.FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new NotFoundException($"La orden {id} no existe.");
        VerificarAcceso(orden, edicion: true);

        if (orden.Estado is not (EstadoOrden.Recibido or EstadoOrden.EnDiagnostico))
            throw new BusinessException("Solo se pueden editar los datos generales de órdenes en estado Recibido o En diagnóstico.");

        var estimada = Validacion.ALocal(dto.FechaEstimadaFinalizacion!.Value);
        if (estimada <= orden.FechaIngreso)
            throw new BusinessException("La fecha estimada debe ser posterior a la fecha de ingreso.");

        _db.ValidarVersion(orden, dto.Version, "La orden");
        orden.DescripcionProblema = dto.DescripcionProblema.Trim();
        orden.FechaEstimadaFinalizacion = estimada;
        MarcarActualizacion(orden);
        await _db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task<OrdenDetalleDto> RegistrarDiagnosticoAsync(int id, DiagnosticoDto dto)
    {
        var orden = await _db.Ordenes.FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new NotFoundException($"La orden {id} no existe.");
        VerificarAcceso(orden, edicion: true);

        if (!_usuario.EsRol(Roles.Mecanico, Roles.Supervisor, Roles.Administrador))
            throw new ForbiddenException("Solo el mecánico asignado, un supervisor o el administrador registran el diagnóstico.");
        if (orden.Estado != EstadoOrden.EnDiagnostico)
            throw new BusinessException("El diagnóstico solo se registra cuando la orden está En diagnóstico.");

        _db.ValidarVersion(orden, dto.Version, "La orden");
        orden.Diagnostico = dto.Diagnostico.Trim();
        MarcarActualizacion(orden);
        _db.HistorialEstados.Add(new HistorialEstadoOrden
        {
            OrdenId = orden.Id,
            EstadoAnterior = orden.Estado,
            EstadoNuevo = orden.Estado,
            Usuario = _usuario.Email,
            Comentario = "Diagnóstico registrado/actualizado."
        });
        await _db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task<OrdenDetalleDto> CambiarEstadoAsync(int id, CambiarEstadoDto dto)
    {
        var nuevo = dto.NuevoEstado!.Value;
        await using var tx = await _db.Database.BeginTransactionAsync();

        var orden = await _db.Ordenes
            .Include(o => o.Presupuesto!).ThenInclude(p => p.Detalles)
            .Include(o => o.Asignaciones)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"La orden {id} no existe.");

        VerificarAcceso(orden, edicion: true);
        _db.ValidarVersion(orden, dto.Version, "La orden");

        if (ReglasEstadoOrden.SoloPorPresupuesto.Contains(nuevo))
            throw new BusinessException($"El estado {nuevo} se asigna automáticamente desde el módulo de presupuestos; no se cambia manualmente.");

        var evaluacion = Evaluar(orden, nuevo);
        if (!evaluacion.Permitido)
        {
            if (evaluacion.Prohibido) throw new ForbiddenException(evaluacion.Motivo);
            throw new BusinessException(evaluacion.Motivo);
        }

        var anterior = orden.Estado;
        switch (nuevo)
        {
            case EstadoOrden.Finalizado:
                orden.FechaFinalizacion = DateTime.Now;
                LiberarAsignaciones(orden);
                break;
            case EstadoOrden.Entregado:
                orden.FechaEntrega = DateTime.Now;
                break;
            case EstadoOrden.Cancelado:
                if (string.IsNullOrWhiteSpace(dto.Comentario) || dto.Comentario.Trim().Length < 5)
                    throw new BusinessException("Indica el motivo de la cancelación (mínimo 5 caracteres).");
                orden.MotivoCancelacion = dto.Comentario.Trim();
                LiberarAsignaciones(orden);
                await DevolverInventarioAsync(orden);
                break;
        }

        orden.Estado = nuevo;
        MarcarActualizacion(orden);
        _db.HistorialEstados.Add(new HistorialEstadoOrden
        {
            OrdenId = orden.Id,
            EstadoAnterior = anterior,
            EstadoNuevo = nuevo,
            Usuario = _usuario.Email,
            Comentario = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim()
        });

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            throw new ConflictException("Otro usuario modificó la orden al mismo tiempo. Recarga e intenta de nuevo.");
        }

        _logger.LogInformation("Orden {Numero}: {Anterior} -> {Nuevo} por {Usuario}", orden.NumeroOrden, anterior, nuevo, _usuario.Email);
        return await ObtenerAsync(id);
    }

    /// <summary>El mecánico registra repuestos usados en la reparación (usa el escenario A de concurrencia).</summary>
    public async Task<MovimientoResultadoDto> RegistrarUsoRepuestoAsync(int ordenId, UsoRepuestoDto dto)
    {
        var orden = await _db.Ordenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException($"La orden {ordenId} no existe.");
        VerificarAcceso(orden, edicion: true);

        if (!_usuario.EsRol(Roles.Mecanico, Roles.Supervisor, Roles.Administrador))
            throw new ForbiddenException("Solo el mecánico asignado o un supervisor registran repuestos utilizados.");
        if (orden.Estado != EstadoOrden.EnReparacion)
            throw new BusinessException("Solo se registran repuestos utilizados cuando la orden está En reparación.");

        var motivo = $"Uso en orden {orden.NumeroOrden}" + (string.IsNullOrWhiteSpace(dto.Observacion) ? "" : $": {dto.Observacion.Trim()}");
        return await _inventario.RegistrarSalidaAsync(dto.RepuestoId, dto.Cantidad, motivo, orden.Id, dto.VersionRepuesto);
    }

    public async Task<List<MovimientoDto>> RepuestosUtilizadosAsync(int ordenId)
    {
        var orden = await _db.Ordenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException($"La orden {ordenId} no existe.");
        VerificarAcceso(orden, edicion: false);
        var movimientos = await _db.Movimientos.AsNoTracking().Include(m => m.Repuesto).Include(m => m.Orden)
            .Where(m => m.OrdenId == ordenId).OrderByDescending(m => m.Fecha).ToListAsync();
        return movimientos.Select(m => m.ToDto()).ToList();
    }

    // ---------------- Reglas de transición ----------------

    public record Evaluacion(bool Permitido, bool Prohibido, string Motivo);

    private Evaluacion Evaluar(OrdenReparacion o, EstadoOrden nuevo)
    {
        if (o.Estado == nuevo)
            return new(false, false, $"La orden ya está en estado {nuevo}.");
        if (!ReglasEstadoOrden.EsValida(o.Estado, nuevo))
        {
            var siguientes = string.Join(", ", ReglasEstadoOrden.SiguientesDe(o.Estado));
            return new(false, false, $"No se puede pasar de {o.Estado} a {nuevo}. Siguiente(s) estado(s) válido(s): {(siguientes.Length == 0 ? "ninguno" : siguientes)}.");
        }

        var tecnico = _usuario.EsRol(Roles.Mecanico, Roles.Supervisor, Roles.Administrador);
        switch (nuevo)
        {
            case EstadoOrden.EnDiagnostico:
                if (o.MecanicoId is null) return new(false, false, "Asigna un mecánico antes de iniciar el diagnóstico.");
                if (!tecnico) return new(false, true, "Solo el mecánico asignado o un supervisor inician el diagnóstico.");
                break;
            case EstadoOrden.EnReparacion:
                if (o.Presupuesto?.Estado != EstadoPresupuesto.Aprobado) return new(false, false, "La reparación solo inicia con el presupuesto aprobado.");
                if (o.MecanicoId is null) return new(false, false, "La orden no tiene mecánico asignado.");
                if (!tecnico) return new(false, true, "Solo el mecánico asignado o un supervisor inician la reparación.");
                break;
            case EstadoOrden.Finalizado:
                if (!(_usuario.EsRol(Roles.Supervisor, Roles.Administrador) && _usuario.TienePermiso(Permisos.OrdenesFinalizar)))
                    return new(false, true, "Solo un Supervisor con el permiso Ordenes.Finalizar puede finalizar reparaciones.");
                break;
            case EstadoOrden.Entregado:
                if (!_usuario.EsRol(Roles.Recepcionista, Roles.Supervisor, Roles.Administrador))
                    return new(false, true, "Solo recepción o un supervisor registran la entrega del vehículo.");
                break;
            case EstadoOrden.Cancelado:
                if (!_usuario.EsRol(Roles.Supervisor, Roles.Administrador))
                    return new(false, true, "Solo un supervisor o el administrador pueden cancelar órdenes.");
                break;
        }
        return new(true, false, "");
    }

    private static void LiberarAsignaciones(OrdenReparacion orden)
    {
        foreach (var a in orden.Asignaciones.Where(a => a.Activa)) a.Activa = false;
    }

    /// <summary>Si se cancela una orden con repuestos ya descontados, se devuelven al inventario.</summary>
    private async Task DevolverInventarioAsync(OrdenReparacion orden)
    {
        var presupuesto = orden.Presupuesto;
        if (presupuesto is null || !presupuesto.InventarioDescontado) return;

        var ids = presupuesto.Detalles.Select(d => d.RepuestoId).ToList();
        var repuestos = await _db.Repuestos.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        foreach (var d in presupuesto.Detalles)
            _inventario.AplicarEntrada(repuestos[d.RepuestoId], d.Cantidad, $"Devolución por cancelación de la orden {orden.NumeroOrden}", orden.Id);
        presupuesto.InventarioDescontado = false;
    }

    private void MarcarActualizacion(OrdenReparacion orden)
    {
        orden.FechaActualizacion = DateTime.Now;
        orden.ActualizadoPor = _usuario.Email;
    }
}
