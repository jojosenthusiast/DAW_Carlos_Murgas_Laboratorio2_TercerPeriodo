# Plan de commits

Sigue el «Orden sugerido para trabajar» del enunciado. Cada paquete agrupa los archivos de un módulo. Los commits
intermedios no necesariamente compilan por separado (por ejemplo, `Program.cs` registra servicios de todos los
módulos); el último commit de cada proyecto deja la solución compilando.

| # | Mensaje | Archivos |
|---|---|---|
| 1 | `chore: crear solución con proyectos API y MVC` | `AutoFix.sln`, `global.json`, `.gitignore`, `*.csproj`, `appsettings*.json`, `Properties/launchSettings.json` de ambos proyectos |
| 2 | `feat(api): entidades y DbContext SQLite con token de concurrencia` | `AutoFix.Api/Entities/`, `Data/AutoFixDbContext.cs`, `Data/ConcurrenciaExtensions.cs` |
| 3 | `feat(api): validaciones, excepciones y mapeos comunes` | `Common/Validacion.cs`, `Common/Excepciones.cs`, `Services/Mapeos.cs` |
| 4 | `feat(api): autenticación JWT con roles y claims` | `Common/Constantes.cs`, `Services/TokenService.cs`, `Services/AuthService.cs`, `Services/UsuarioActual.cs`, `DTOs/AuthDtos.cs`, `Controllers/AuthController.cs`, `Controllers/UsuariosController.cs` |
| 5 | `feat(api): CRUD de clientes y vehículos` | `DTOs/ClienteDtos.cs`, `Controllers/ClientesController.cs`, `Controllers/VehiculosController.cs` |
| 6 | `feat(api): órdenes de reparación, estados y mecánicos` | `DTOs/OrdenDtos.cs`, `DTOs/MecanicoDtos.cs`, `Services/ReglasEstadoOrden.cs`, `Services/OrdenService.cs`, `Controllers/OrdenesController.cs`, `Controllers/MecanicosController.cs` |
| 7 | `feat(api): presupuestos con descuento automático de inventario` | `DTOs/PresupuestoDtos.cs`, `Services/PresupuestoService.cs`, `Controllers/PresupuestosController.cs` |
| 8 | `feat(api): carga de imágenes y evidencias` | `DTOs/EvidenciaReporteDtos.cs`, `Services/ImagenService.cs`, `Controllers/EvidenciasController.cs`, `wwwroot/uploads/.gitkeep` |
| 9 | `feat(api): inventario y concurrencia (escenarios A y B)` | `DTOs/InventarioDtos.cs`, `Services/InventarioService.cs`, `Services/AsignacionService.cs`, `Controllers/RepuestosController.cs` |
| 10 | `feat(api): middlewares de Correlation ID, excepciones y logs` | `Middlewares/`, `Services/LogPersistenciaService.cs`, `Controllers/ReportesController.cs`, `Program.cs` |
| 11 | `feat(api): migración inicial y datos de prueba` | `Migrations/`, `Data/DbInicializador.cs`, `AutoFix.Api.http` |
| 12 | `feat(mvc): consumo de la API con HttpClient y sesión con JWT` | `AutoFix.Mvc/Infraestructura/`, `Models/`, `Program.cs` |
| 13 | `feat(mvc): diseño base, login, registro y dashboard` | `Views/Shared/`, `Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml`, `wwwroot/`, `Controllers/CuentaController.cs`, `Controllers/HomeController.cs`, `Views/Cuenta/`, `Views/Home/` |
| 14 | `feat(mvc): vistas de clientes y vehículos` | Controladores y vistas de Clientes y Vehiculos |
| 15 | `feat(mvc): órdenes, seguimiento y presupuestos` | Controladores y vistas de Ordenes y Presupuestos |
| 16 | `feat(mvc): mecánicos, inventario, movimientos y evidencias` | Controladores y vistas de Mecanicos, Inventario, Movimientos y Evidencias |
| 17 | `feat(mvc): reportes y bitácora de logs` | `Controllers/ReportesController.cs`, `Views/Reportes/` |
| 18 | `test: pruebas automáticas de los 12 casos y concurrencia` | `herramientas/AutoFix.Pruebas/` |
| 19 | `docs: README, evidencias e instrucciones` | `README.md`, `CLAUDE.md`, `evidencias/`, `herramientas/PLAN_COMMITS.md`, `herramientas/crear-commits.sh` |

El script agrega al final un commit `chore: ajustes finales` solo si quedó algún archivo sin incluir.
