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
public class EvidenciasController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly ImagenService _imagenes;
    private readonly OrdenService _ordenes;
    private readonly IUsuarioActual _usuario;

    public EvidenciasController(AutoFixDbContext db, ImagenService imagenes, OrdenService ordenes, IUsuarioActual usuario)
    {
        _db = db; _imagenes = imagenes; _ordenes = ordenes; _usuario = usuario;
    }

    /// <summary>
    /// Sube una fotografía (recepción, daño, repuesto utilizado o reparación finalizada).
    /// multipart/form-data: Archivo (JPG/JPEG/PNG, máx. 5 MB), Tipo, Descripcion.
    /// </summary>
    [HttpPost("api/ordenes/{ordenId:int}/evidencias")]
    [Authorize(Policy = Politicas.PersonalTaller)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<EvidenciaDto>> Subir(int ordenId, [FromForm] SubirEvidenciaDto dto)
    {
        var orden = await _db.Ordenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException("La orden no existe.");
        _ordenes.VerificarAcceso(orden, edicion: true);

        if (orden.Estado == EstadoOrden.Cancelado)
            throw new BusinessException("No se pueden agregar evidencias a una orden cancelada.");
        if (dto.Tipo == TipoEvidencia.ReparacionFinalizada && orden.Estado is not (EstadoOrden.EnReparacion or EstadoOrden.Finalizado or EstadoOrden.Entregado))
            throw new BusinessException("Las fotos de reparación finalizada se registran cuando la orden está En reparación o Finalizada.");

        var archivo = await _imagenes.GuardarAsync(dto.Archivo, "evidencias");
        var evidencia = new Evidencia
        {
            OrdenId = orden.Id,
            Tipo = dto.Tipo!.Value,
            Ruta = archivo.Ruta,          // en SQLite solo se guarda la ruta
            NombreOriginal = archivo.NombreOriginal,
            TamanoBytes = archivo.TamanoBytes,
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            SubidoPor = _usuario.Email
        };
        _db.Evidencias.Add(evidencia);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _imagenes.Eliminar(archivo.Ruta);
            throw;
        }
        return CreatedAtAction(nameof(ListarPorOrden), new { ordenId }, evidencia.ToDto(orden.NumeroOrden));
    }

    [HttpGet("api/ordenes/{ordenId:int}/evidencias")]
    public async Task<ActionResult<List<EvidenciaDto>>> ListarPorOrden(int ordenId)
    {
        var orden = await _db.Ordenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == ordenId)
                    ?? throw new NotFoundException("La orden no existe.");
        _ordenes.VerificarAcceso(orden, edicion: false);
        var lista = await _db.Evidencias.AsNoTracking().Where(e => e.OrdenId == ordenId).OrderByDescending(e => e.FechaSubida).ToListAsync();
        return lista.Select(e => e.ToDto(orden.NumeroOrden)).ToList();
    }

    /// <summary>Galería general de evidencias (personal del taller).</summary>
    [HttpGet("api/evidencias")]
    [Authorize(Policy = Politicas.PersonalTaller)]
    public async Task<ActionResult<List<EvidenciaDto>>> Listar([FromQuery] int? ordenId, [FromQuery] TipoEvidencia? tipo)
    {
        var q = _db.Evidencias.AsNoTracking().Include(e => e.Orden).AsQueryable();
        if (_usuario.EsRol(Roles.Mecanico))
        {
            var propio = _usuario.MecanicoId ?? -1;
            q = q.Where(e => e.Orden!.MecanicoId == propio);
        }
        if (ordenId.HasValue) { var o = ordenId.Value; q = q.Where(e => e.OrdenId == o); }
        if (tipo.HasValue) { var t = tipo.Value; q = q.Where(e => e.Tipo == t); }
        var lista = await q.OrderByDescending(e => e.FechaSubida).Take(300).ToListAsync();
        return lista.Select(e => e.ToDto()).ToList();
    }

    [HttpDelete("api/evidencias/{id:int}")]
    [Authorize(Roles = Roles.Supervisor + "," + Roles.Administrador)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var e = await _db.Evidencias.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("La evidencia no existe.");
        var ruta = e.Ruta;
        _db.Evidencias.Remove(e);
        await _db.SaveChangesAsync();
        _imagenes.Eliminar(ruta);
        return NoContent();
    }
}
