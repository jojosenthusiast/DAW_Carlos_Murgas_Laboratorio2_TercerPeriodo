using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

[Authorize(Roles = Roles.Personal)]
public class EvidenciasController : ControladorBase
{
    public async Task<IActionResult> Index(int? ordenId, string? tipo)
    {
        ViewBag.OrdenId = ordenId;
        ViewBag.Tipo = tipo;
        var url = QueryHelpers.AddQueryString("api/evidencias", new Dictionary<string, string?>
        {
            ["ordenId"] = ordenId?.ToString(),
            ["tipo"] = tipo
        });
        return View(await Api.GetAsync<List<EvidenciaVm>>(url));
    }

    [HttpGet]
    public async Task<IActionResult> Subir(int? ordenId)
    {
        var vm = new EvidenciaFormVm { OrdenId = ordenId ?? 0 };
        await CargarOrdenesAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Subir(EvidenciaFormVm vm)
    {
        var error = ValidarImagen(vm.Archivo);
        if (error is not null) ModelState.AddModelError(nameof(vm.Archivo), error);
        if (!ModelState.IsValid) { await CargarOrdenesAsync(vm); return View(vm); }
        try
        {
            await Api.PostArchivoAsync<EvidenciaVm>($"api/ordenes/{vm.OrdenId}/evidencias", vm.Archivo!,
                new Dictionary<string, string?> { ["Tipo"] = vm.Tipo, ["Descripcion"] = vm.Descripcion });
            Exito("Fotografía registrada.");
            return RedirectToAction(nameof(Index), new { ordenId = vm.OrdenId });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex) || ex.Status == 413)
        {
            CargarErrores(ex);
            await CargarOrdenesAsync(vm);
            return View(vm);
        }
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, int? ordenId)
    {
        try
        {
            await Api.DeleteAsync($"api/evidencias/{id}");
            Exito("Fotografía eliminada.");
        }
        catch (ApiException ex) when (ex.Status == 404)
        {
            Error(ex.Mensaje);
        }
        return RedirectToAction(nameof(Index), new { ordenId });
    }

    private async Task CargarOrdenesAsync(EvidenciaFormVm vm) =>
        vm.Ordenes = (await Api.GetAsync<List<OrdenResumenVm>>("api/ordenes"))
            .Where(o => o.Estado != "Cancelado").ToList();
}
