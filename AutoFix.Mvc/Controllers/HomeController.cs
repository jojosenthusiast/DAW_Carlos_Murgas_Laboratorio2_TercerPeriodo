using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFix.Mvc.Controllers;

public class HomeController : ControladorBase
{
    /// <summary>Dashboard: resumen del taller (adaptado al rol).</summary>
    public async Task<IActionResult> Index() =>
        View(await Api.GetAsync<DashboardVm>("api/reportes/dashboard"));

    [AllowAnonymous]
    public IActionResult Error() => View("ErrorApi", new ErrorApiVm("Ocurrió un error inesperado en la aplicación.", 500));
}
