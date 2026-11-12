#!/usr/bin/env bash
# Crea los commits del laboratorio en orden (ver herramientas/PLAN_COMMITS.md).
# Uso (desde la raíz del repositorio): bash herramientas/crear-commits.sh
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -d .git ]; then
  git init -b main
fi

if [ ! -d AutoFix.Api/Migrations ]; then
  echo "Falta AutoFix.Api/Migrations. Ejecuta primero: cd AutoFix.Api && dotnet ef migrations add InicialAutoFix" >&2
  exit 1
fi

API=AutoFix.Api
MVC=AutoFix.Mvc

commit() {
  local mensaje="$1"; shift
  local rutas=()
  for r in "$@"; do
    if [ -e "$r" ]; then rutas+=("$r"); fi
  done
  if [ ${#rutas[@]} -gt 0 ]; then git add -- "${rutas[@]}"; fi
  if git diff --cached --quiet; then
    echo "(sin cambios) $mensaje"
  else
    git commit -q -m "$mensaje"
    echo "OK  $mensaje"
  fi
}

commit "chore: crear solución con proyectos API y MVC" \
  AutoFix.sln global.json .gitignore \
  $API/AutoFix.Api.csproj $API/appsettings.json $API/appsettings.Development.json $API/Properties \
  $MVC/AutoFix.Mvc.csproj $MVC/appsettings.json $MVC/appsettings.Development.json $MVC/Properties

commit "feat(api): entidades y DbContext SQLite con token de concurrencia" \
  $API/Entities $API/Data/AutoFixDbContext.cs $API/Data/ConcurrenciaExtensions.cs

commit "feat(api): validaciones, excepciones y mapeos comunes" \
  $API/Common/Validacion.cs $API/Common/Excepciones.cs $API/Services/Mapeos.cs

commit "feat(api): autenticación JWT con roles y claims" \
  $API/Common/Constantes.cs $API/Services/TokenService.cs $API/Services/AuthService.cs $API/Services/UsuarioActual.cs \
  $API/DTOs/AuthDtos.cs $API/Controllers/AuthController.cs $API/Controllers/UsuariosController.cs

commit "feat(api): CRUD de clientes y vehículos" \
  $API/DTOs/ClienteDtos.cs $API/Controllers/ClientesController.cs $API/Controllers/VehiculosController.cs

commit "feat(api): órdenes de reparación, estados y mecánicos" \
  $API/DTOs/OrdenDtos.cs $API/DTOs/MecanicoDtos.cs $API/Services/ReglasEstadoOrden.cs $API/Services/OrdenService.cs \
  $API/Controllers/OrdenesController.cs $API/Controllers/MecanicosController.cs

commit "feat(api): presupuestos con descuento automático de inventario" \
  $API/DTOs/PresupuestoDtos.cs $API/Services/PresupuestoService.cs $API/Controllers/PresupuestosController.cs

commit "feat(api): carga de imágenes y evidencias" \
  $API/DTOs/EvidenciaReporteDtos.cs $API/Services/ImagenService.cs $API/Controllers/EvidenciasController.cs \
  $API/wwwroot/uploads/.gitkeep

commit "feat(api): inventario y concurrencia (escenarios A y B)" \
  $API/DTOs/InventarioDtos.cs $API/Services/InventarioService.cs $API/Services/AsignacionService.cs \
  $API/Controllers/RepuestosController.cs

commit "feat(api): middlewares de Correlation ID, excepciones y logs" \
  $API/Middlewares $API/Services/LogPersistenciaService.cs $API/Controllers/ReportesController.cs $API/Program.cs

commit "feat(api): migración inicial y datos de prueba" \
  $API/Migrations $API/Data/DbInicializador.cs $API/AutoFix.Api.http

commit "feat(mvc): consumo de la API con HttpClient y sesión con JWT" \
  $MVC/Infraestructura $MVC/Models $MVC/Program.cs

commit "feat(mvc): diseño base, login, registro y dashboard" \
  $MVC/Views/Shared $MVC/Views/_ViewImports.cshtml $MVC/Views/_ViewStart.cshtml $MVC/wwwroot \
  $MVC/Controllers/CuentaController.cs $MVC/Controllers/HomeController.cs $MVC/Views/Cuenta $MVC/Views/Home

commit "feat(mvc): vistas de clientes y vehículos" \
  $MVC/Controllers/ClientesController.cs $MVC/Controllers/VehiculosController.cs $MVC/Views/Clientes $MVC/Views/Vehiculos

commit "feat(mvc): órdenes, seguimiento y presupuestos" \
  $MVC/Controllers/OrdenesController.cs $MVC/Controllers/PresupuestosController.cs $MVC/Views/Ordenes $MVC/Views/Presupuestos

commit "feat(mvc): mecánicos, inventario, movimientos y evidencias" \
  $MVC/Controllers/MecanicosController.cs $MVC/Controllers/InventarioController.cs $MVC/Controllers/EvidenciasController.cs \
  $MVC/Views/Mecanicos $MVC/Views/Inventario $MVC/Views/Movimientos $MVC/Views/Evidencias

commit "feat(mvc): reportes y bitácora de logs" \
  $MVC/Controllers/ReportesController.cs $MVC/Views/Reportes

commit "test: pruebas automáticas de los 12 casos y concurrencia" \
  herramientas/AutoFix.Pruebas

commit "docs: README, evidencias e instrucciones" \
  README.md CLAUDE.md evidencias herramientas/PLAN_COMMITS.md herramientas/crear-commits.sh

git add -A
if ! git diff --cached --quiet; then
  git commit -q -m "chore: ajustes finales"
  echo "OK  chore: ajustes finales"
fi

echo
git log --oneline
