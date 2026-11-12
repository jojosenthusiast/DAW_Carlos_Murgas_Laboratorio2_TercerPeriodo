using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

[Authorize(Roles = Roles.Personal)]
public class InventarioController : ControladorBase
{
    public async Task<IActionResult> Index(string? buscar, bool soloAlertas = false)
    {
        ViewBag.Buscar = buscar;
        ViewBag.SoloAlertas = soloAlertas;
        var url = QueryHelpers.AddQueryString("api/repuestos", new Dictionary<string, string?>
        {
            ["buscar"] = buscar,
            ["soloAlertas"] = soloAlertas ? "true" : null,
            ["incluirInactivos"] = User.Puede(Permisos.InventarioGestionar) ? "true" : null
        });
        return View(await Api.GetAsync<List<RepuestoVm>>(url));
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var repuesto = await Api.GetAsync<RepuestoVm>($"api/repuestos/{id}");
        return View(new InventarioDetallePaginaVm
        {
            Repuesto = repuesto,
            Movimientos = await Api.GetAsync<List<MovimientoVm>>($"api/repuestos/{id}/movimientos"),
            Movimiento = new MovimientoFormVm { RepuestoId = id, Version = repuesto.Version }
        });
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpGet]
    public IActionResult Crear() => View("Formulario", new RepuestoFormVm());

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(RepuestoFormVm vm)
    {
        if (!ModelState.IsValid) return View("Formulario", vm);
        try
        {
            var r = await Api.PostAsync<RepuestoVm>("api/repuestos", vm);
            Exito($"Repuesto {r.Codigo} registrado.");
            return RedirectToAction(nameof(Detalle), new { id = r.Id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var r = await Api.GetAsync<RepuestoVm>($"api/repuestos/{id}");
        return View("Formulario", new RepuestoFormVm
        {
            Id = r.Id, Version = r.Version, Codigo = r.Codigo, Nombre = r.Nombre, Descripcion = r.Descripcion,
            Categoria = r.Categoria, Marca = r.Marca, Precio = r.Precio, Existencias = r.Existencias, StockMinimo = r.StockMinimo
        });
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, RepuestoFormVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return View("Formulario", vm);
        try
        {
            await Api.PutAsync<RepuestoVm>($"api/repuestos/{id}", vm);
            Exito("Repuesto actualizado.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            return View("Formulario", vm);
        }
    }

    /// <summary>Entrada o salida de inventario (salida protegida por concurrencia en la API).</summary>
    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Movimiento(MovimientoFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            Error("Revisa la cantidad y el motivo del movimiento.");
            return RedirectToAction(nameof(Detalle), new { id = vm.RepuestoId });
        }
        var ruta = vm.Tipo == "Salida" ? "salidas" : "entradas";
        try
        {
            var r = await Api.PostAsync<MovimientoResultadoVm>($"api/repuestos/{vm.RepuestoId}/{ruta}",
                new { vm.Cantidad, vm.Motivo, vm.Version });
            Exito($"{(vm.Tipo == "Salida" ? "Salida" : "Entrada")} registrada. Existencias: {r.Repuesto.Existencias}.");
            if (r.AlertaStockMinimo) TempData["Aviso"] = r.MensajeAlerta;
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            Error(ex.MensajeCompleto);
        }
        return RedirectToAction(nameof(Detalle), new { id = vm.RepuestoId });
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Foto(int id, IFormFile? archivo)
    {
        var error = ValidarImagen(archivo);
        if (error is not null) { Error(error); return RedirectToAction(nameof(Detalle), new { id }); }
        try
        {
            await Api.PostArchivoAsync<RepuestoVm>($"api/repuestos/{id}/foto", archivo!);
            Exito("Fotografía del repuesto actualizada.");
        }
        catch (ApiException ex) when (ex.Status is 400 or 404 or 413)
        {
            Error(ex.Mensaje);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize(Roles = Roles.Mandos)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Estado(int id, bool activo)
    {
        try
        {
            await Api.PatchAsync<RepuestoVm>($"api/repuestos/{id}/estado", new { activo });
            Exito(activo ? "Repuesto activado." : "Repuesto desactivado.");
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            Error(ex.Mensaje);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}

[Authorize(Roles = Roles.Personal)]
public class MovimientosController : ControladorBase
{
    public async Task<IActionResult> Index(int? repuestoId, string? tipo, DateTime? desde, DateTime? hasta)
    {
        ViewBag.RepuestoId = repuestoId;
        ViewBag.Tipo = tipo;
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        ViewBag.Repuestos = await Api.GetAsync<List<RepuestoVm>>("api/repuestos?incluirInactivos=true");
        var url = QueryHelpers.AddQueryString("api/movimientos", new Dictionary<string, string?>
        {
            ["repuestoId"] = repuestoId?.ToString(),
            ["tipo"] = tipo,
            ["desde"] = desde?.ToString("yyyy-MM-dd"),
            ["hasta"] = hasta?.ToString("yyyy-MM-dd")
        });
        return View(await Api.GetAsync<List<MovimientoVm>>(url));
    }
}
