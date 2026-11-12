using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AutoFix.Api.Common;
using AutoFix.Api.Data;
using AutoFix.Api.Middlewares;
using AutoFix.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------- Llave JWT: nunca en el repositorio ----------
// Se lee de user-secrets o variable de entorno (Jwt__Key). En Development, si no existe, se genera una temporal.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Configura Jwt:Key (mínimo 32 caracteres) con user-secrets o la variable de entorno Jwt__Key.");
    jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    builder.Configuration["Jwt:Key"] = jwtKey;
    Console.WriteLine("ADVERTENCIA: Jwt:Key no configurada; se generó una llave temporal (los tokens se invalidan al reiniciar).");
}
var jwt = builder.Configuration.GetSection("Jwt");

// ---------- Base de datos SQLite ----------
builder.Services.AddDbContext<AutoFixDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ---------- Servicios ----------
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JwtOptions>(jwt);
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ImagenService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<OrdenService>();
builder.Services.AddScoped<AsignacionService>();
builder.Services.AddScoped<PresupuestoService>();
builder.Services.AddSingleton<LogPersistenciaService>();

// ---------- Autenticación JWT ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // conserva los nombres "role", "permiso", "sub", "email"
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
        o.Events = new JwtBearerEvents
        {
            // Respuestas 401/403 con el mismo formato JSON que el resto de errores.
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                if (ctx.Response.HasStarted) return;
                var mensaje = ctx.AuthenticateFailure is SecurityTokenExpiredException
                    ? "El token expiró. Inicia sesión nuevamente."
                    : "Debes enviar un token JWT válido (Authorization: Bearer ...).";
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new ErrorRespuesta(401, "No autorizado", mensaje,
                    ctx.HttpContext.Items["CorrelationId"]?.ToString()));
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new ErrorRespuesta(403, "Prohibido",
                    "No tienes el rol o el permiso (claim) necesario para esta acción.",
                    ctx.HttpContext.Items["CorrelationId"]?.ToString()));
            }
        };
    });

// ---------- Autorización: roles + claims ----------
builder.Services.AddAuthorization(Politicas.Registrar);

// ---------- Límite de intentos de login (protección contra fuerza bruta) ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (ctx, token) =>
    {
        await ctx.HttpContext.Response.WriteAsJsonAsync(new ErrorRespuesta(429, "Demasiadas solicitudes",
            "Demasiados intentos de inicio de sesión. Espera un minuto.",
            ctx.HttpContext.Items["CorrelationId"]?.ToString()), token);
    };
});

// ---------- Controladores ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        // Errores de validación (400) con el formato común e incluyendo el Correlation ID.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errores = ctx.ModelState
                .Where(kv => kv.Value is not null && kv.Value.Errors.Count > 0)
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Valor inválido." : e.ErrorMessage).ToArray());
            return new BadRequestObjectResult(new ErrorRespuesta(400, "Datos inválidos", "Revisa los datos enviados.",
                ctx.HttpContext.Items["CorrelationId"]?.ToString(), errores));
        };
    });

// ---------- Swagger con soporte para JWT ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AutoFix API", Version = "v1", Description = "Gestión de órdenes de reparación automotriz" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Pega solo el token (sin la palabra Bearer)."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Migraciones + datos de prueba
await DbInicializador.InicializarAsync(app.Services);

// ---------- Orden de los middlewares (según el enunciado) ----------
// 1) Correlation ID -> 2) Excepciones -> 3) Registro de solicitudes -> 4) Autenticación -> 5) Autorización -> 6) Controladores
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExcepcionesMiddleware>();
app.UseMiddleware<RegistroSolicitudesMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoFix API v1"));
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // sirve wwwroot/uploads (las imágenes)

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
