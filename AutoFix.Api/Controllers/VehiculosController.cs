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
[Route("api/vehiculos")]
public class VehiculosController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly ImagenService _imagenes;

    public VehiculosController(AutoFixDbContext db, IUsuarioActual usuario, ImagenService imagenes)
    {
        _db = db; _usuario = usuario; _imagenes = imagenes;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.PersonalTaller)]
    public async Task<ActionResult<List<VehiculoDto>>> Listar([FromQuery] int? clienteId, [FromQuery] string? buscar)
    {
        var q = _db.Vehiculos.AsNoTracking().Include(v => v.Cliente).AsQueryable();
        if (clienteId.HasValue) { var c = clienteId.Value; q = q.Where(v => v.ClienteId == c); }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = $"%{buscar.Trim()}%";
            q = q.Where(v => EF.Functions.Like(v.Placa, b) || EF.Functions.Like(v.Marca, b)
                          || EF.Functions.Like(v.Modelo, b) || EF.Functions.Like(v.Cliente!.NombreCompleto, b));
        }
        var lista = await q.OrderBy(v => v.Placa).ToListAsync();
        return lista.Select(v => v.ToDto()).ToList();
    }

    /// <summary>Vehículos del cliente autenticado.</summary>
    [HttpGet("mios")]
    [Authorize(Roles = Roles.Cliente)]
    public async Task<ActionResult<List<VehiculoDto>>> Mios()
    {
        var propio = _usuario.ClienteId ?? -1;
        var lista = await _db.Vehiculos.AsNoTracking().Include(v => v.Cliente).Where(v => v.ClienteId == propio).OrderBy(v => v.Placa).ToListAsync();
        return lista.Select(v => v.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehiculoDto>> Obtener(int id)
    {
        var v = await _db.Vehiculos.AsNoTracking().Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("El vehículo no existe.");
        if (_usuario.EsRol(Roles.Cliente) && v.ClienteId != _usuario.ClienteId)
            throw new ForbiddenException("Solo puedes consultar tus propios vehículos.");
        return v.ToDto();
    }

    [HttpPost]
    [Authorize(Policy = Politicas.GestionClientes)]
    public async Task<ActionResult<VehiculoDto>> Crear(VehiculoGuardarDto dto)
    {
        if (!await _db.Clientes.AnyAsync(c => c.Id == dto.ClienteId)) throw new NotFoundException("El cliente propietario no existe.");
        await ValidarUnicosAsync(_db, Validacion.Normalizar(dto.Placa), Validacion.Normalizar(dto.NumeroChasis), null);

        var vehiculo = Nuevo(dto);
        vehiculo.ClienteId = dto.ClienteId;
        _db.Vehiculos.Add(vehiculo);
        await _db.SaveChangesAsync();
        await _db.Entry(vehiculo).Reference(v => v.Cliente).LoadAsync();
        return CreatedAtAction(nameof(Obtener), new { id = vehiculo.Id }, vehiculo.ToDto());
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.GestionClientes)]
    public async Task<ActionResult<VehiculoDto>> Actualizar(int id, VehiculoGuardarDto dto)
    {
        var v = await _db.Vehiculos.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("El vehículo no existe.");
        if (!await _db.Clientes.AnyAsync(c => c.Id == dto.ClienteId)) throw new NotFoundException("El cliente propietario no existe.");
        if (v.ClienteId != dto.ClienteId && await _db.Ordenes.AnyAsync(o => o.VehiculoId == id && o.Estado != EstadoOrden.Entregado && o.Estado != EstadoOrden.Cancelado))
            throw new ConflictException("No se puede cambiar el propietario mientras el vehículo tenga órdenes activas.");

        var placa = Validacion.Normalizar(dto.Placa);
        var chasis = Validacion.Normalizar(dto.NumeroChasis);
        await ValidarUnicosAsync(_db, placa, chasis, id);

        v.Placa = placa;
        v.Marca = dto.Marca.Trim();
        v.Modelo = dto.Modelo.Trim();
        v.Anio = dto.Anio;
        v.Color = dto.Color.Trim();
        v.NumeroChasis = chasis;
        v.ClienteId = dto.ClienteId;
        await _db.SaveChangesAsync();
        await _db.Entry(v).Reference(x => x.Cliente).LoadAsync();
        return v.ToDto();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var v = await _db.Vehiculos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("El vehículo no existe.");
        if (await _db.Ordenes.AnyAsync(o => o.VehiculoId == id))
            throw new ConflictException("No se puede eliminar un vehículo con órdenes de reparación registradas.");
        var foto = v.FotoRuta;
        _db.Vehiculos.Remove(v);
        await _db.SaveChangesAsync();
        _imagenes.Eliminar(foto);
        return NoContent();
    }

    /// <summary>Fotografía del vehículo (multipart/form-data, JPG/PNG, máx. 5 MB).</summary>
    [HttpPost("{id:int}/foto")]
    [Authorize(Policy = Politicas.PersonalTaller)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<VehiculoDto>> SubirFoto(int id, [FromForm] SubirFotoDto dto)
    {
        var v = await _db.Vehiculos.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("El vehículo no existe.");
        var archivo = await _imagenes.GuardarAsync(dto.Archivo, "vehiculos");
        var anterior = v.FotoRuta;
        v.FotoRuta = archivo.Ruta;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _imagenes.Eliminar(archivo.Ruta); // compensación: no dejar archivos huérfanos
            throw;
        }
        _imagenes.Eliminar(anterior);
        return v.ToDto();
    }

    // ---------- Auxiliares compartidos ----------

    internal static Vehiculo Nuevo(VehiculoNuevoDto dto) => new()
    {
        Placa = Validacion.Normalizar(dto.Placa),
        Marca = dto.Marca.Trim(),
        Modelo = dto.Modelo.Trim(),
        Anio = dto.Anio,
        Color = dto.Color.Trim(),
        NumeroChasis = Validacion.Normalizar(dto.NumeroChasis)
    };

    internal static async Task ValidarUnicosAsync(AutoFixDbContext db, string placa, string chasis, int? excluirId)
    {
        if (await db.Vehiculos.AnyAsync(v => v.Placa == placa && v.Id != (excluirId ?? 0)))
            throw new ConflictException($"Ya existe un vehículo con la placa {placa}.");
        if (await db.Vehiculos.AnyAsync(v => v.NumeroChasis == chasis && v.Id != (excluirId ?? 0)))
            throw new ConflictException($"Ya existe un vehículo con el número de chasis {chasis}.");
    }
}
