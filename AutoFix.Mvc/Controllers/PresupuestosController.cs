using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

public class PresupuestosController : ControladorBase
{
    [Authorize(Roles = Roles.Personal)]
    public async Task<IActionResult> Index(string? estado)
    {
        ViewBag.Estado = estado;
        var url = QueryHelpers.AddQueryString("api/presupuestos", new Dictionary<string, string?> { ["estado"] = estado });
        return View(await Api.GetAsync<List<PresupuestoVm>>(url));
    }

    /// <summary>El cliente también puede ver su presupuesto y si fue aprobado.</summary>
    public async Task<IActionResult> Detalle(int id) => View(await Api.GetAsync<PresupuestoVm>($"api/presupuestos/{id}"));

    [Authorize(Roles = "Administrador,Supervisor,Mecanico")]
    [HttpGet]
    public async Task<IActionResult> Crear(int ordenId)
    {
        var orden = await Api.GetAsync<OrdenDetalleVm>($"api/ordenes/{ordenId}");
        if (orden.PresupuestoId.HasValue) return RedirectToAction(nameof(Editar), new { id = orden.PresupuestoId });
        var vm = new PresupuestoFormVm { OrdenId = ordenId, NumeroOrden = orden.NumeroOrden };
        vm.Detalles.Add(new LineaPresupuestoVm());
        await CargarRepuestosAsync(vm);
        return View("Formulario", vm);
    }

    [Authorize(Roles = "Administrador,Supervisor,Mecanico")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(PresupuestoFormVm vm)
    {
        vm.Detalles = vm.Detalles.Where(d => d.RepuestoId > 0).ToList();
        if (!ModelState.IsValid) { await CargarRepuestosAsync(vm); return View("Formulario", vm); }
        try
        {
            var p = await Api.PostAsync<PresupuestoVm>($"api/ordenes/{vm.OrdenId}/presupuesto",
                new { vm.ManoObra, vm.Observaciones, vm.Detalles });
            Exito($"Presupuesto creado por {Formato.Dinero(p.Total)}. Revísalo y envíalo a aprobación.");
            return RedirectToAction(nameof(Detalle), new { id = p.Id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex) || ex.Status == 403)
        {
            CargarErrores(ex);
            await CargarRepuestosAsync(vm);
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = "Administrador,Supervisor,Mecanico")]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var p = await Api.GetAsync<PresupuestoVm>($"api/presupuestos/{id}");
        var vm = new PresupuestoFormVm
        {
            Id = p.Id, OrdenId = p.OrdenId, NumeroOrden = p.NumeroOrden, Version = p.Version,
            ManoObra = p.ManoObra, Observaciones = p.Observaciones,
            Detalles = p.Detalles.Select(d => new LineaPresupuestoVm { RepuestoId = d.RepuestoId, Cantidad = d.Cantidad }).ToList()
        };
        if (vm.Detalles.Count == 0) vm.Detalles.Add(new LineaPresupuestoVm());
        await CargarRepuestosAsync(vm);
        return View("Formulario", vm);
    }

    [Authorize(Roles = "Administrador,Supervisor,Mecanico")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, PresupuestoFormVm vm)
    {
        vm.Id = id;
        vm.Detalles = vm.Detalles.Where(d => d.RepuestoId > 0).ToList();
        if (!ModelState.IsValid) { await CargarRepuestosAsync(vm); return View("Formulario", vm); }
        try
        {
            await Api.PutAsync<PresupuestoVm>($"api/presupuestos/{id}", new { vm.ManoObra, vm.Observaciones, vm.Detalles, vm.Version });
            Exito("Presupuesto actualizado.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex) || ex.Status == 403)
        {
            CargarErrores(ex);
            await CargarRepuestosAsync(vm);
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Enviar(int id, Guid version) =>
        Decidir(id, $"api/presupuestos/{id}/enviar", new { version }, "Presupuesto enviado a aprobación.");

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Aprobar(int id, Guid version, string? comentario) =>
        Decidir(id, $"api/presupuestos/{id}/aprobar", new { version, comentario }, "Presupuesto aprobado; el inventario se descontó automáticamente.");

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Rechazar(int id, Guid version, string? comentario) =>
        Decidir(id, $"api/presupuestos/{id}/rechazar", new { version, comentario }, "Presupuesto rechazado; puede corregirse y reenviarse.");

    private async Task<IActionResult> Decidir(int id, string url, object cuerpo, string mensaje)
    {
        try
        {
            await Api.PostAsync<PresupuestoVm>(url, cuerpo);
            Exito(mensaje);
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            Error(ex.MensajeCompleto);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private async Task CargarRepuestosAsync(PresupuestoFormVm vm) =>
        vm.Repuestos = await Api.GetAsync<List<RepuestoVm>>("api/repuestos");
}
