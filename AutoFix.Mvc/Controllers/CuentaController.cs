using System.Security.Claims;
using AutoFix.Mvc.Infraestructura;
using AutoFix.Mvc.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFix.Mvc.Controllers;

public class CuentaController : ControladorBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl, bool expirado = false)
    {
        if (User.Identity?.IsAuthenticated == true && !expirado) return RedirectToAction("Index", "Home");
        if (expirado) ViewBag.Aviso = "Tu sesión expiró. Inicia sesión nuevamente.";
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginVm());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(vm);
        try
        {
            var respuesta = await Api.PostAsync<LoginRespuestaVm>("api/auth/login", new { vm.Email, vm.Password });
            await IniciarSesionAsync(respuesta);
            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
        catch (ApiException ex) when (ex.Status is 400 or 401 or 403 or 429)
        {
            ModelState.AddModelError(string.Empty, ex.Mensaje);
            return View(vm);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Registro() => View(new RegistroVm());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(RegistroVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        try
        {
            var respuesta = await Api.PostAsync<LoginRespuestaVm>("api/auth/registro", vm);
            await IniciarSesionAsync(respuesta);
            Exito("Tu cuenta fue creada. Aquí verás el avance de tus vehículos.");
            return RedirectToAction("Index", "Home");
        }
        catch (ApiException ex) when (ex.Status is 400 or 409 or 429)
        {
            CargarErrores(ex);
            return View(vm);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccesoDenegado() => View();

    /// <summary>Guarda el JWT y los datos del usuario dentro de la cookie de sesión.</summary>
    private async Task IniciarSesionAsync(LoginRespuestaVm r)
    {
        var u = r.Usuario;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.NombreCompleto),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Rol),
            new(Claves.Token, r.Token)
        };
        claims.AddRange(u.Permisos.Select(p => new Claim(Claves.Permiso, p)));
        if (u.ClienteId.HasValue) claims.Add(new Claim(Claves.ClienteId, u.ClienteId.Value.ToString()));
        if (u.MecanicoId.HasValue) claims.Add(new Claim(Claves.MecanicoId, u.MecanicoId.Value.ToString()));

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTime.SpecifyKind(r.ExpiraUtc, DateTimeKind.Utc)
            });
    }
}
