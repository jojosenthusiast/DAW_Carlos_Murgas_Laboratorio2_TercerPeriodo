using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

public class OrdenesController : ControladorBase
{
    public async Task<IActionResult> Index(string? estado, string? buscar)
    {
        ViewBag.Estado = estado;
        ViewBag.Buscar = buscar;
        var url = QueryHelpers.AddQueryString("api/ordenes", new Dictionary<string, string?> { ["estado"] = estado, ["buscar"] = buscar });
        return View(await Api.GetAsync<List<OrdenResumenVm>>(url));
    }

    /// <summary>Detalle de la orden: seguimiento, diagnóstico, estados, asignación, repuestos y evidencias.</summary>
    public async Task<IActionResult> Detalle(int id)
    {
        var pagina = new OrdenDetallePaginaVm { Orden = await Api.GetAsync<OrdenDetalleVm>($"api/ordenes/{id}") };

        if (pagina.Orden.PresupuestoId.HasValue)
            pagina.Presupuesto = await Api.GetAsync<PresupuestoVm>($"api/presupuestos/{pagina.Orden.PresupuestoId}");

        if (User.EsPersonal())
        {
            if (User.Puede(Permisos.OrdenesAsignar))
                pagina.Mecanicos = await Api.GetAsync<List<MecanicoVm>>("api/mecanicos?soloActivos=true");
            if (pagina.Orden.Estado == "EnReparacion")
                pagina.Repuestos = await Api.GetAsync<List<RepuestoVm>>("api/repuestos");
            pagina.RepuestosUtilizados = await Api.GetAsync<List<MovimientoVm>>($"api/ordenes/{id}/repuestos");
        }
        return View(pagina);
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpGet]
    public async Task<IActionResult> Crear(int? clienteId, int? vehiculoId)
    {
        await CargarClientesAsync();
        return View(new OrdenCrearVm { ClienteId = clienteId ?? 0, VehiculoId = vehiculoId ?? 0 });
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(OrdenCrearVm vm)
    {
        if (!ModelState.IsValid) { await CargarClientesAsync(); return View(vm); }
        try
        {
            var orden = await Api.PostAsync<OrdenDetalleVm>("api/ordenes", vm);
            Exito($"Orden {orden.NumeroOrden} creada en estado Recibido.");
            return RedirectToAction(nameof(Detalle), new { id = orden.Id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex) || ex.Status == 403)
        {
            CargarErrores(ex);
            await CargarClientesAsync();
            return View(vm);
        }
    }

    /// <summary>Usado por el formulario de creación para cargar los vehículos del cliente elegido.</summary>
    [Authorize(Roles = Roles.Personal)]
    [HttpGet]
    public async Task<IActionResult> VehiculosDeCliente(int clienteId)
    {
        var vehiculos = await Api.GetAsync<List<VehiculoVm>>($"api/vehiculos?clienteId={clienteId}");
        return Json(vehiculos.Select(v => new { v.Id, Texto = $"{v.Placa}: {v.Marca} {v.Modelo} {v.Anio}" }));
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var o = await Api.GetAsync<OrdenDetalleVm>($"api/ordenes/{id}");
        return View(new OrdenEditarVm
        {
            Id = o.Id, NumeroOrden = o.NumeroOrden, Version = o.Version,
            DescripcionProblema = o.DescripcionProblema, FechaEstimadaFinalizacion = o.FechaEstimadaFinalizacion
        });
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, OrdenEditarVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return View(vm);
        try
        {
            await Api.PutAsync<OrdenDetalleVm>($"api/ordenes/{id}",
                new { vm.DescripcionProblema, vm.FechaEstimadaFinalizacion, vm.Version });
            Exito("Orden actualizada.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            return View(vm);
        }
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Diagnostico(int id, string diagnostico, Guid version)
    {
        await Ejecutar(id, () => Api.PutAsync<OrdenDetalleVm>($"api/ordenes/{id}/diagnostico", new { diagnostico, version }),
            "Diagnóstico guardado.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado, string? comentario, Guid version)
    {
        await Ejecutar(id, () => Api.PatchAsync<OrdenDetalleVm>($"api/ordenes/{id}/estado", new { nuevoEstado, comentario, version }),
            $"La orden pasó a «{Formato.Estado(nuevoEstado)}».");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(int id, string? comentario, Guid version)
    {
        await Ejecutar(id, () => Api.PostAsync<OrdenDetalleVm>($"api/ordenes/{id}/finalizar", new { comentario, version }),
            "Reparación finalizada.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Asignar(int id, int mecanicoId, DateTime? inicio, DateTime? fin, Guid? versionMecanico, Guid version)
    {
        if (mecanicoId <= 0 || inicio is null || fin is null)
        {
            Error("Selecciona el mecánico y el horario de inicio y fin.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        await Ejecutar(id, () => Api.PostAsync<AsignacionVm>($"api/ordenes/{id}/asignar",
                new { mecanicoId, inicio, fin, versionMecanico, versionOrden = version }),
            "Mecánico asignado.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UsarRepuesto(int id, int repuestoId, int cantidad, Guid? versionRepuesto, string? observacion)
    {
        try
        {
            var r = await Api.PostAsync<MovimientoResultadoVm>($"api/ordenes/{id}/repuestos",
                new { repuestoId, cantidad, versionRepuesto, observacion });
            Exito($"Se descontaron {cantidad} unidad(es) de {r.Repuesto.Codigo}. Existencias: {r.Repuesto.Existencias}." +
                  (r.AlertaStockMinimo ? $" {r.MensajeAlerta}" : ""));
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            Error(ex.MensajeCompleto);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Evidencia(int id, IFormFile? archivo, string tipo, string? descripcion)
    {
        var error = ValidarImagen(archivo);
        if (error is not null) { Error(error); return RedirectToAction(nameof(Detalle), new { id }); }
        await Ejecutar(id, () => Api.PostArchivoAsync<EvidenciaVm>($"api/ordenes/{id}/evidencias", archivo!,
                new Dictionary<string, string?> { ["Tipo"] = tipo, ["Descripcion"] = descripcion }),
            "Fotografía registrada.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Ejecuta una acción sobre la orden y convierte errores esperados en mensajes.</summary>
    private async Task Ejecutar<T>(int id, Func<Task<T>> accion, string mensajeExito)
    {
        try
        {
            await accion();
            Exito(mensajeExito);
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex) || ex.Status == 413)
        {
            Error(ex.MensajeCompleto);
        }
    }

    private async Task CargarClientesAsync()
    {
        var clientes = await Api.GetAsync<List<ClienteVm>>("api/clientes");
        ViewBag.Clientes = clientes.Where(c => c.CantidadVehiculos > 0)
            .Select(c => new SelectListItem($"{c.NombreCompleto} ({c.Dui})", c.Id.ToString())).ToList();
    }
}
