using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoFix.Mvc.Controllers;

[Authorize(Roles = "Administrador,Supervisor,Recepcionista")]
public class ReportesController : ControladorBase
{
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta)
    {
        var d = (desde ?? DateTime.Today.AddDays(-30)).Date;
        var h = (hasta ?? DateTime.Today).Date;
        if (h < d) (d, h) = (h, d);
        var rango = $"desde={d:yyyy-MM-dd}&hasta={h:yyyy-MM-dd}";

        return View(new ReportesPaginaVm
        {
            Desde = d,
            Hasta = h,
            Ordenes = await Api.GetAsync<ReporteOrdenesVm>($"api/reportes/ordenes?{rango}"),
            Inventario = await Api.GetAsync<ReporteInventarioVm>($"api/reportes/inventario?{rango}"),
            Mecanicos = await Api.GetAsync<List<CargaMecanicoVm>>("api/reportes/mecanicos")
        });
    }

    /// <summary>Bitácora de solicitudes: usuario, endpoint, código y Correlation ID.</summary>
    public async Task<IActionResult> Logs(string? correlationId, int? codigo, bool soloErrores = false, string? usuario = null)
    {
        ViewBag.CorrelationId = correlationId;
        ViewBag.Codigo = codigo;
        ViewBag.SoloErrores = soloErrores;
        ViewBag.Usuario = usuario;
        var url = QueryHelpers.AddQueryString("api/logs", new Dictionary<string, string?>
        {
            ["correlationId"] = correlationId,
            ["codigo"] = codigo?.ToString(),
            ["soloErrores"] = soloErrores ? "true" : null,
            ["usuario"] = usuario,
            ["top"] = "300"
        });
        return View(await Api.GetAsync<List<LogVm>>(url));
    }
}
