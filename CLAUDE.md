# Instrucciones para Claude Code: AutoFix

Este repositorio contiene un laboratorio universitario completo (ASP.NET Core Web API + MVC, .NET 10, EF Core + SQLite).
El código se escribió en un entorno **sin SDK de .NET**, así que nunca se compiló. Tu trabajo es: compilarlo, corregir
errores de compilación sin cambiar el comportamiento, generar la migración, ejecutarlo, verificar con las pruebas
automáticas y crear los commits en el orden indicado.

## Reglas que no se deben romper

- `AutoFix.Mvc` **no** puede referenciar EF Core, `AutoFix.Api` ni la base de datos. Solo consume la API con `HttpClient`.
- No guardar contraseñas ni llaves reales en archivos versionados. La llave JWT va en `dotnet user-secrets`.
- No cambiar nombres de roles, claims (`permiso` / `Ordenes.Crear`, etc.), rutas de la API ni el orden de los middlewares:
  CorrelationId -> Excepciones -> RegistroSolicitudes -> Autenticación -> Autorización -> Controladores.
- La concurrencia usa `Guid Version` con `[ConcurrencyCheck]` (SQLite no tiene RowVersion). No reemplazarlo.
- Al corregir errores, cambia lo mínimo y explica cada corrección en el mensaje del commit correspondiente.

## Paso 1. Entorno

```bash
dotnet --version                          # debe ser 10.x
dotnet tool install --global dotnet-ef    # o: dotnet tool update --global dotnet-ef
dotnet dev-certs https --trust
```

## Paso 2. Compilar y corregir

```bash
dotnet restore AutoFix.sln
dotnet build AutoFix.sln
```

Puntos con más probabilidad de requerir ajuste:

1. **Versiones de paquetes** en `AutoFix.Api/AutoFix.Api.csproj` (10.0.0). Si alguna no existe en NuGet, usa la última 10.0.x.
2. **Swashbuckle 10 / Microsoft.OpenApi 2.x** en `AutoFix.Api/Program.cs` (`using Microsoft.OpenApi;`,
   `OpenApiSecuritySchemeReference`, `AddSecurityRequirement(document => ...)`). Si la versión instalada usa la API
   anterior, adapta solo ese bloque; el resultado debe seguir siendo un botón Authorize con esquema Bearer.
3. Advertencias de nulabilidad: no son bloqueantes; no las silencies globalmente.

## Paso 3. Migración y base de datos

```bash
cd AutoFix.Api
dotnet user-secrets set "Jwt:Key" "autofix-llave-local-de-desarrollo-con-mas-de-32-caracteres"
dotnet ef migrations add InicialAutoFix
dotnet ef database update
cd ..
```

La carpeta `AutoFix.Api/Migrations/` **sí** se versiona. `autofix.db` no (está en `.gitignore`).

## Paso 4. Ejecutar y probar

```bash
# Terminal 1: API (aplica migraciones y siembra usuarios de prueba al iniciar)
dotnet run --project AutoFix.Api --launch-profile https
# Esperar a que https://localhost:7180/swagger/index.html responda 200.

# Terminal 2: pruebas automáticas (12 pruebas obligatorias + concurrencia)
dotnet run --project herramientas/AutoFix.Pruebas -- todo https://localhost:7180/ | tee evidencias/resultado-pruebas.txt
# Código de salida esperado: 0. Si alguna verificación falla, corrige la causa en la API y repite.

# Terminal 3: MVC
dotnet run --project AutoFix.Mvc --launch-profile https
# Verificación mínima:
curl -k -s -o /dev/null -w "%{http_code}\n" https://localhost:7280/Cuenta/Login   # 200
curl -k -s -o /dev/null -w "%{http_code}\n" https://localhost:7280/               # 302 (redirige al login)
```

Si quieres repetir desde cero: detén la API, borra `AutoFix.Api/autofix.db*` y vuelve a ejecutarla.
La API permite 20 inicios de sesión por minuto por IP; si las pruebas responden 429, espera un minuto.

Verificación manual sugerida en el MVC (https://localhost:7280): iniciar sesión como `recepcion@autofix.com`,
crear cliente y vehículo, crear orden; como `supervisor@autofix.com` asignar mecánico, pasar a diagnóstico,
registrar diagnóstico, elaborar presupuesto, enviarlo y aprobarlo; subir una foto; como `cliente@autofix.com`
comprobar que solo ve sus datos y no tiene botones de edición.

## Paso 5. Commits

Ejecuta `bash herramientas/crear-commits.sh` desde la raíz (en Windows, con Git Bash). Crea el repositorio si no existe
y hace los commits en el orden del laboratorio (ver `herramientas/PLAN_COMMITS.md`). Debe ejecutarse **después** de
generar la migración y de que `dotnet build` funcione, para que las correcciones queden incluidas.

Después:

```bash
git remote add origin <URL-del-repositorio-en-GitHub>
git push -u origin main
```

## Paso 6. Evidencias

`evidencias/resultado-pruebas.txt` lo genera el paso 4. Las capturas de pantalla (lista en `evidencias/README.md`)
las debe tomar la persona; puedes recordárselo al terminar.
