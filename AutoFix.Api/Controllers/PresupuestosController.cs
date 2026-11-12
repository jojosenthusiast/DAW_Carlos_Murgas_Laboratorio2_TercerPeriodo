using AutoFix.Api.Common;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFix.Api.Controllers;

[ApiController]
[Route("api/presupuestos")]
public class PresupuestosController : ControllerBase
{
    private readonly PresupuestoService _presupuestos;
    public PresupuestosController(PresupuestoService presupuestos) => _presupuestos = presupuestos;

    [HttpGet]
    [Authorize(Policy = Politicas.PersonalTaller)]
    public async Task<ActionResult<List<PresupuestoDto>>> Listar([FromQuery] EstadoPresupuesto? estado) =>
        await _presupuestos.ListarAsync(estado);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PresupuestoDto>> Obtener(int id) => await _presupuestos.ObtenerAsync(id);

    /// <summary>El cliente consulta el presupuesto de su orden y si fue aprobado.</summary>
    [HttpGet("/api/ordenes/{ordenId:int}/presupuesto")]
    public async Task<ActionResult<PresupuestoDto>> ObtenerPorOrden(int ordenId) => await _presupuestos.ObtenerPorOrdenAsync(ordenId);

    [HttpPost("/api/ordenes/{ordenId:int}/presupuesto")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<PresupuestoDto>> Crear(int ordenId, PresupuestoGuardarDto dto)
    {
        var p = await _presupuestos.CrearAsync(ordenId, dto);
        return CreatedAtAction(nameof(Obtener), new { id = p.Id }, p);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<PresupuestoDto>> Actualizar(int id, PresupuestoGuardarDto dto) => await _presupuestos.ActualizarAsync(id, dto);

    [HttpPost("{id:int}/enviar")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<PresupuestoDto>> Enviar(int id, [FromBody] DecisionPresupuestoDto? dto) =>
        await _presupuestos.EnviarAprobacionAsync(id, dto);

    /// <summary>Solo Supervisor/Administrador con el permiso Ordenes.Aprobar.</summary>
    [Authorize(Policy = Politicas.PuedeAprobar)]
    [HttpPost("{id:int}/aprobar")]
    public async Task<ActionResult<PresupuestoDto>> Aprobar(int id, [FromBody] DecisionPresupuestoDto? dto) =>
        await _presupuestos.AprobarAsync(id, dto);

    [Authorize(Policy = Politicas.PuedeAprobar)]
    [HttpPost("{id:int}/rechazar")]
    public async Task<ActionResult<PresupuestoDto>> Rechazar(int id, DecisionPresupuestoDto dto) =>
        await _presupuestos.RechazarAsync(id, dto);
}
