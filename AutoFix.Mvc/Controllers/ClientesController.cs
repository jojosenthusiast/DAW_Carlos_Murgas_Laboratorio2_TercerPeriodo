using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

public class ClientesController : ControladorBase
{
    [Authorize(Roles = Roles.Personal)]
    public async Task<IActionResult> Index(string? buscar)
    {
        ViewBag.Buscar = buscar;
        var url = QueryHelpers.AddQueryString("api/clientes", new Dictionary<string, string?> { ["buscar"] = buscar });
        return View(await Api.GetAsync<List<ClienteVm>>(url));
    }

    public async Task<IActionResult> Detalle(int id) =>
        View(await Api.GetAsync<ClienteDetalleVm>($"api/clientes/{id}"));

    [Authorize(Roles = Roles.Cliente)]
    public IActionResult MiPerfil()
    {
        var id = User.ClienteId();
        return id.HasValue ? RedirectToAction(nameof(Detalle), new { id }) : RedirectToAction("Index", "Home");
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpGet]
    public IActionResult Crear() => View("Formulario", new ClienteFormVm());

    [Authorize(Roles = Roles.Gestion)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ClienteFormVm vm)
    {
        if (!ModelState.IsValid) return View("Formulario", vm);
        try
        {
            var creado = await Api.PostAsync<ClienteDetalleVm>("api/clientes", vm);
            Exito($"Cliente {creado.Cliente.NombreCompleto} registrado. Ahora agrega sus vehículos.");
            return RedirectToAction(nameof(Detalle), new { id = creado.Cliente.Id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var c = (await Api.GetAsync<ClienteDetalleVm>($"api/clientes/{id}")).Cliente;
        return View("Formulario", new ClienteFormVm
        {
            Id = c.Id, NombreCompleto = c.NombreCompleto, Dui = c.Dui, Telefono = c.Telefono, Correo = c.Correo, Direccion = c.Direccion
        });
    }

    [Authorize(Roles = Roles.Gestion)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ClienteFormVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return View("Formulario", vm);
        try
        {
            await Api.PutAsync<ClienteDetalleVm>($"api/clientes/{id}", vm);
            Exito("Datos del cliente actualizados.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await Api.DeleteAsync($"api/clientes/{id}");
            Exito("Cliente eliminado.");
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (ex.Status is 404 or 409)
        {
            Error(ex.Mensaje);
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }
}
