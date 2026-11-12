using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

public class VehiculosController : ControladorBase
{
    public async Task<IActionResult> Index(int? clienteId, string? buscar)
    {
        ViewBag.Buscar = buscar;
        if (User.IsInRole(Roles.Cliente))
            return View(await Api.GetAsync<List<VehiculoVm>>("api/vehiculos/mios"));

        var url = QueryHelpers.AddQueryString("api/vehiculos", new Dictionary<string, string?>
        {
            ["clienteId"] = clienteId?.ToString(),
            ["buscar"] = buscar
        });
        return View(await Api.GetAsync<List<VehiculoVm>>(url));
    }

    public async Task<IActionResult> Detalle(int id) => View(await Api.GetAsync<VehiculoVm>($"api/vehiculos/{id}"));

    [Authorize(Roles = Roles.Gestion)]
    [HttpGet]
    public async Task<IActionResult> Crear(int? clienteId)
    {
        await CargarClientesAsync();
        return View("Formulario", new VehiculoFormVm { ClienteId = clienteId ?? 0 });
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(VehiculoFormVm vm)
    {
        if (!ModelState.IsValid) { await CargarClientesAsync(); return View("Formulario", vm); }
        try
        {
            var v = await Api.PostAsync<VehiculoVm>("api/vehiculos", vm);
            Exito($"Vehículo {v.Placa} registrado.");
            return RedirectToAction("Detalle", "Clientes", new { id = v.ClienteId });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            await CargarClientesAsync();
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var v = await Api.GetAsync<VehiculoVm>($"api/vehiculos/{id}");
        await CargarClientesAsync();
        return View("Formulario", new VehiculoFormVm
        {
            Id = v.Id, ClienteId = v.ClienteId, Placa = v.Placa, Marca = v.Marca, Modelo = v.Modelo,
            Anio = v.Anio, Color = v.Color, NumeroChasis = v.NumeroChasis
        });
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, VehiculoFormVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) { await CargarClientesAsync(); return View("Formulario", vm); }
        try
        {
            await Api.PutAsync<VehiculoVm>($"api/vehiculos/{id}", vm);
            Exito("Vehículo actualizado.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            await CargarClientesAsync();
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, int clienteId)
    {
        try
        {
            await Api.DeleteAsync($"api/vehiculos/{id}");
            Exito("Vehículo eliminado.");
            return RedirectToAction("Detalle", "Clientes", new { id = clienteId });
        }
        catch (ApiException ex) when (ex.Status is 404 or 409)
        {
            Error(ex.Mensaje);
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }

    [Authorize(Roles = Roles.Personal)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Foto(int id, IFormFile? archivo)
    {
        var error = ValidarImagen(archivo);
        if (error is not null) { Error(error); return RedirectToAction(nameof(Detalle), new { id }); }
        try
        {
            await Api.PostArchivoAsync<VehiculoVm>($"api/vehiculos/{id}/foto", archivo!);
            Exito("Fotografía del vehículo actualizada.");
        }
        catch (ApiException ex) when (ex.Status is 400 or 404 or 413)
        {
            Error(ex.Mensaje);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private async Task CargarClientesAsync()
    {
        var clientes = await Api.GetAsync<List<ClienteVm>>("api/clientes");
        ViewBag.Clientes = clientes.Select(c => new SelectListItem($"{c.NombreCompleto} ({c.Dui})", c.Id.ToString())).ToList();
    }
}
