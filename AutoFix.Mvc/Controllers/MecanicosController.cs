using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFix.Mvc.Controllers;

[Authorize(Roles = Roles.Personal)]
public class MecanicosController : ControladorBase
{
    public async Task<IActionResult> Index() => View(await Api.GetAsync<List<MecanicoVm>>("api/mecanicos"));

    /// <summary>Asignaciones del mecánico y consulta de disponibilidad en un horario.</summary>
    public async Task<IActionResult> Detalle(int id, DateTime? inicio, DateTime? fin)
    {
        ViewBag.Mecanico = await Api.GetAsync<MecanicoVm>($"api/mecanicos/{id}");
        if (inicio.HasValue && fin.HasValue)
        {
            try
            {
                ViewBag.Disponibilidad = await Api.GetAsync<DisponibilidadVm>(
                    $"api/mecanicos/{id}/disponibilidad?inicio={inicio:yyyy-MM-ddTHH:mm}&fin={fin:yyyy-MM-ddTHH:mm}");
            }
            catch (ApiException ex) when (ex.Status == 400)
            {
                TempData["Error"] = ex.Mensaje;
            }
        }
        ViewBag.Inicio = inicio;
        ViewBag.Fin = fin;
        return View(await Api.GetAsync<List<AsignacionVm>>($"api/mecanicos/{id}/asignaciones"));
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet]
    public IActionResult Crear() => View("Formulario", new MecanicoFormVm());

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(MecanicoFormVm vm)
    {
        if (!ModelState.IsValid) return View("Formulario", vm);
        try
        {
            var m = await Api.PostAsync<MecanicoVm>("api/mecanicos", vm);
            Exito($"Mecánico {m.NombreCompleto} registrado" + (m.TieneUsuario ? " con acceso al sistema." : "."));
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            CargarErrores(ex);
            return View("Formulario", vm);
        }
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var m = await Api.GetAsync<MecanicoVm>($"api/mecanicos/{id}");
        return View("Formulario", new MecanicoFormVm
        {
            Id = m.Id, Version = m.Version, NombreCompleto = m.NombreCompleto, Especialidad = m.Especialidad, Telefono = m.Telefono, Email = m.Email
        });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, MecanicoFormVm vm)
    {
        vm.Id = id;
        vm.PasswordUsuario = null;
        ModelState.Remove(nameof(vm.PasswordUsuario));
        if (!ModelState.IsValid) return View("Formulario", vm);
        try
        {
            await Api.PutAsync<MecanicoVm>($"api/mecanicos/{id}", new { vm.NombreCompleto, vm.Especialidad, vm.Telefono, vm.Email, vm.Version });
            Exito("Datos del mecánico actualizados.");
            return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Estado(int id, bool activo)
    {
        try
        {
            await Api.PatchAsync<MecanicoVm>($"api/mecanicos/{id}/estado", new { activo });
            Exito(activo ? "Mecánico activado." : "Mecánico desactivado.");
        }
        catch (ApiException ex) when (EsErrorDeFormulario(ex))
        {
            Error(ex.Mensaje);
        }
        return RedirectToAction(nameof(Index));
    }
}
