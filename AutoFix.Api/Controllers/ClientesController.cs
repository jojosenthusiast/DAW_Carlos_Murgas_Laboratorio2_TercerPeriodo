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
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly AutoFixDbContext _db;
    private readonly IUsuarioActual _usuario;

    public ClientesController(AutoFixDbContext db, IUsuarioActual usuario)
    {
        _db = db; _usuario = usuario;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.PersonalTaller)]
    public async Task<ActionResult<List<ClienteDto>>> Listar([FromQuery] string? buscar)
    {
        var q = _db.Clientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = $"%{buscar.Trim()}%";
            q = q.Where(c => EF.Functions.Like(c.NombreCompleto, b) || EF.Functions.Like(c.Dui, b)
                          || EF.Functions.Like(c.Correo, b) || EF.Functions.Like(c.Telefono, b));
        }
        return await q.OrderBy(c => c.NombreCompleto)
            .Select(c => new ClienteDto(c.Id, c.NombreCompleto, c.Dui, c.Telefono, c.Correo, c.Direccion, c.FechaRegistro, c.Vehiculos.Count()))
            .ToListAsync();
    }

    /// <summary>El cliente autenticado consulta su propia ficha.</summary>
    [HttpGet("mi-perfil")]
    [Authorize(Roles = Roles.Cliente)]
    public async Task<ActionResult<ClienteDetalleDto>> MiPerfil()
    {
        var id = _usuario.ClienteId ?? throw new NotFoundException("Tu usuario no está vinculado a un cliente.");
        return await ConstruirDetalle(id);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDetalleDto>> Obtener(int id)
    {
        if (_usuario.EsRol(Roles.Cliente) && _usuario.ClienteId != id)
            throw new ForbiddenException("Solo puedes consultar tus propios datos.");
        return await ConstruirDetalle(id);
    }

    /// <summary>Crea un cliente; opcionalmente con sus vehículos en la misma transacción.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.GestionClientes)]
    public async Task<ActionResult<ClienteDetalleDto>> Crear(ClienteGuardarDto dto)
    {
        var dui = Validacion.Normalizar(dto.Dui);
        var correo = Validacion.Correo(dto.Correo);
        if (await _db.Clientes.AnyAsync(c => c.Dui == dui)) throw new ConflictException($"Ya existe un cliente con el DUI {dui}.");
        if (await _db.Clientes.AnyAsync(c => c.Correo == correo)) throw new ConflictException($"Ya existe un cliente con el correo {correo}.");

        var cliente = new Cliente
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Dui = dui,
            Telefono = dto.Telefono.Trim(),
            Correo = correo,
            Direccion = dto.Direccion.Trim()
        };

        var vehiculos = dto.Vehiculos ?? new List<VehiculoNuevoDto>();
        var placas = vehiculos.Select(v => Validacion.Normalizar(v.Placa)).ToList();
        var chasis = vehiculos.Select(v => Validacion.Normalizar(v.NumeroChasis)).ToList();
        if (placas.Distinct().Count() != placas.Count) throw new BusinessException("Hay placas repetidas en la solicitud.");
        if (chasis.Distinct().Count() != chasis.Count) throw new BusinessException("Hay números de chasis repetidos en la solicitud.");
        foreach (var v in vehiculos)
        {
            await VehiculosController.ValidarUnicosAsync(_db, Validacion.Normalizar(v.Placa), Validacion.Normalizar(v.NumeroChasis), null);
            cliente.Vehiculos.Add(VehiculosController.Nuevo(v));
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(Obtener), new { id = cliente.Id }, (await ConstruirDetalle(cliente.Id)).Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.GestionClientes)]
    public async Task<ActionResult<ClienteDetalleDto>> Actualizar(int id, ClienteGuardarDto dto)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == id) ?? throw new NotFoundException("El cliente no existe.");
        var dui = Validacion.Normalizar(dto.Dui);
        var correo = Validacion.Correo(dto.Correo);
        if (await _db.Clientes.AnyAsync(c => c.Dui == dui && c.Id != id)) throw new ConflictException($"Ya existe otro cliente con el DUI {dui}.");
        if (await _db.Clientes.AnyAsync(c => c.Correo == correo && c.Id != id)) throw new ConflictException($"Ya existe otro cliente con el correo {correo}.");

        cliente.NombreCompleto = dto.NombreCompleto.Trim();
        cliente.Dui = dui;
        cliente.Telefono = dto.Telefono.Trim();
        cliente.Correo = correo;
        cliente.Direccion = dto.Direccion.Trim();
        await _db.SaveChangesAsync();
        return await ConstruirDetalle(id);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var cliente = await _db.Clientes.Include(c => c.Vehiculos).FirstOrDefaultAsync(c => c.Id == id)
                      ?? throw new NotFoundException("El cliente no existe.");
        if (await _db.Ordenes.AnyAsync(o => o.ClienteId == id))
            throw new ConflictException("No se puede eliminar un cliente con órdenes de reparación registradas.");
        _db.Clientes.Remove(cliente);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult<ClienteDetalleDto>> ConstruirDetalle(int id)
    {
        var c = await _db.Clientes.AsNoTracking().AsSplitQuery()
                    .Include(x => x.Vehiculos)
                    .Include(x => x.Ordenes).ThenInclude(o => o.Vehiculo)
                    .Include(x => x.Ordenes).ThenInclude(o => o.Mecanico)
                    .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("El cliente no existe.");

        var dto = new ClienteDto(c.Id, c.NombreCompleto, c.Dui, c.Telefono, c.Correo, c.Direccion, c.FechaRegistro, c.Vehiculos.Count);
        foreach (var v in c.Vehiculos) v.Cliente = c;
        foreach (var o in c.Ordenes) o.Cliente = c;
        return new ClienteDetalleDto(dto,
            c.Vehiculos.OrderBy(v => v.Placa).Select(v => v.ToDto()).ToList(),
            c.Ordenes.OrderByDescending(o => o.FechaIngreso).Select(o => o.ToResumen()).ToList());
    }
}
