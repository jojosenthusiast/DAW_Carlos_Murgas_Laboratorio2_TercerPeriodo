using AutoFix.Api.Common;
using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFix.Api.Controllers;

[ApiController]
[Route("api/ordenes")]
public class OrdenesController : ControllerBase
{
    private readonly OrdenService _ordenes;
    private readonly AsignacionService _asignaciones;

    public OrdenesController(OrdenService ordenes, AsignacionService asignaciones)
    {
        _ordenes = ordenes; _asignaciones = asignaciones;
    }

    /// <summary>Cliente: sus órdenes. Mecánico: sus asignadas. Personal: todas.</summary>
    [HttpGet]
    public async Task<ActionResult<List<OrdenResumenDto>>> Listar([FromQuery] EstadoOrden? estado, [FromQuery] string? buscar,
        [FromQuery] int? mecanicoId, [FromQuery] int? clienteId) =>
        await _ordenes.ListarAsync(estado, buscar, mecanicoId, clienteId);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrdenDetalleDto>> Obtener(int id) => await _ordenes.ObtenerAsync(id);

    [HttpPost]
    [Authorize(Policy = Politicas.PuedeCrearOrden)]
    public async Task<ActionResult<OrdenDetalleDto>> Crear(CrearOrdenDto dto)
    {
        var orden = await _ordenes.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = orden.Id }, orden);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<OrdenDetalleDto>> Editar(int id, EditarOrdenDto dto) => await _ordenes.EditarAsync(id, dto);

    [HttpPut("{id:int}/diagnostico")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<OrdenDetalleDto>> Diagnostico(int id, DiagnosticoDto dto) => await _ordenes.RegistrarDiagnosticoAsync(id, dto);

    /// <summary>Cambio de estado validado en el backend (nadie salta pasos).</summary>
    [HttpPatch("{id:int}/estado")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<OrdenDetalleDto>> CambiarEstado(int id, CambiarEstadoDto dto) => await _ordenes.CambiarEstadoAsync(id, dto);

    /// <summary>Finalizar la reparación: Supervisor/Administrador con el claim Ordenes.Finalizar.</summary>
    [HttpPost("{id:int}/finalizar")]
    [Authorize(Policy = Politicas.PuedeFinalizar)]
    public async Task<ActionResult<OrdenDetalleDto>> Finalizar(int id, [FromBody] DecisionPresupuestoDto? dto) =>
        await _ordenes.CambiarEstadoAsync(id, new CambiarEstadoDto
        {
            NuevoEstado = EstadoOrden.Finalizado,
            Comentario = dto?.Comentario ?? "Reparación finalizada.",
            Version = dto?.Version
        });

    /// <summary>Escenario B: asignar mecánico con validación de horario y concurrencia.</summary>
    [HttpPost("{id:int}/asignar")]
    [Authorize(Policy = Politicas.PuedeAsignar)]
    public async Task<ActionResult<AsignacionDto>> Asignar(int id, AsignarMecanicoDto dto) => await _asignaciones.AsignarAsync(id, dto);

    /// <summary>Escenario A desde la orden: el mecánico registra repuestos utilizados.</summary>
    [HttpPost("{id:int}/repuestos")]
    [Authorize(Policy = Politicas.PuedeEditarOrden)]
    public async Task<ActionResult<MovimientoResultadoDto>> UsarRepuesto(int id, UsoRepuestoDto dto) =>
        await _ordenes.RegistrarUsoRepuestoAsync(id, dto);

    [HttpGet("{id:int}/repuestos")]
    public async Task<ActionResult<List<MovimientoDto>>> RepuestosUtilizados(int id) => await _ordenes.RepuestosUtilizadosAsync(id);
}
