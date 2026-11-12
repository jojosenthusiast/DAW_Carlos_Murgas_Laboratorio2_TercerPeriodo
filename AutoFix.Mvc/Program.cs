using System.Globalization;
using AutoFix.Mvc.Infraestructura;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add<ApiExcepcionFilter>();
    // Toda la aplicación exige sesión salvo [AllowAnonymous] (login y registro).
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
});

// ---------- Consumo de la API con HttpClient (el MVC nunca toca la base de datos) ----------
var baseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Falta Api:BaseUrl en appsettings.json");
if (!baseUrl.EndsWith('/')) baseUrl += "/";

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TokenHandler>();
builder.Services.AddHttpClient("AutoFixApi", client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
}).AddHttpMessageHandler<TokenHandler>();
builder.Services.AddScoped<ApiCliente>();
builder.Services.AddSingleton<RutasApi>();

// ---------- El JWT se guarda dentro de la cookie de autenticación (cifrada y HttpOnly) ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Cuenta/Login";
        o.LogoutPath = "/Cuenta/Salir";
        o.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        o.Cookie.Name = "AutoFix.Sesion";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.SlidingExpiration = false; // la sesión dura lo mismo que el token
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Cultura fija: el punto decimal coincide con la API y con la validación del navegador.
var cultura = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new[] { cultura },
    SupportedUICultures = new[] { cultura }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
