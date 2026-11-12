// Pruebas automáticas contra la API en ejecución.
// Uso:  dotnet run --project herramientas/AutoFix.Pruebas -- [todo|humo|concurrencia] [https://localhost:7180/]
// Código de salida: 0 = todo correcto, 1 = alguna prueba falló, 2 = la API no responde.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

var modo = args.FirstOrDefault(a => !a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? "todo";
var baseUrl = args.FirstOrDefault(a => a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? "https://localhost:7180/";
if (!baseUrl.EndsWith('/')) baseUrl += "/";

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"AutoFix - pruebas automáticas ({modo}) contra {baseUrl}");
Console.WriteLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

var probador = new Probador(baseUrl);
try
{
    if (modo is "humo" or "todo") await probador.HumoAsync();
    if (modo is "concurrencia" or "todo") await probador.ConcurrenciaAsync();
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"\nNo se pudo conectar con la API en {baseUrl}. ¿Está en ejecución? Detalle: {ex.Message}");
    return 2;
}
return probador.Resumen();

class Probador
{
    private const string PngValido =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private readonly string _base;
    private readonly string _sufijo = Random.Shared.Next(100000, 999999).ToString();
    private readonly List<(string Nombre, bool Ok, string Detalle)> _resultados = new();
    private readonly Dictionary<string, HttpClient> _sesiones = new();

    public Probador(string baseUrl) => _base = baseUrl;

    public record Resp(int Status, JsonNode? Json, string Cuerpo, string CorrelationId);

    // =====================================================================
    // Las 12 pruebas obligatorias del enunciado
    // =====================================================================
    public async Task HumoAsync()
    {
        Titulo("PRUEBAS OBLIGATORIAS");
        var anonimo = NuevoCliente();

        var sinToken = await Enviar(anonimo, HttpMethod.Get, "api/clientes");
        Verificar("11. Acceso a la API sin JWT -> 401", sinToken.Status == 401, Detalle(sinToken));

        var claveMala = await Enviar(anonimo, HttpMethod.Post, "api/auth/login", new { email = "admin@autofix.com", password = "Incorrecta#1" });
        Verificar("Login con contraseña incorrecta -> 401", claveMala.Status == 401, Detalle(claveMala));

        var admin = await Sesion("admin@autofix.com", "Admin#2026");
        var recepcion = await Sesion("recepcion@autofix.com", "Recepcion#2026");
        var limitada = await Sesion("recepcion.limitada@autofix.com", "Recepcion#2026");
        var supervisor = await Sesion("supervisor@autofix.com", "Supervisor#2026");
        var cliente = await Sesion("cliente@autofix.com", "Cliente#2026");
        await Sesion("mecanico1@autofix.com", "Mecanico#2026");
        Verificar("Inicio de sesión de cada rol (JWT emitido)", true, "Administrador, Recepcionista, Supervisor, Mecánico y Cliente");

        // 1. Cliente con dos vehículos
        var nuevoCliente = await Enviar(recepcion, HttpMethod.Post, "api/clientes", ClienteDePrueba("PR"));
        var vehiculos = nuevoCliente.Json?["vehiculos"]?.AsArray();
        Verificar("1. Registrar un cliente con dos vehículos", nuevoCliente.Status == 201 && vehiculos?.Count == 2, Detalle(nuevoCliente));
        if (nuevoCliente.Status != 201 || vehiculos is null) return;
        var clienteId = Int(nuevoCliente.Json!["cliente"]!["id"]);
        var vehiculoId = Int(vehiculos[0]!["id"]);

        // 2. Orden en estado Recibido + duplicada rechazada
        var estimada = DateTime.Now.AddDays(5).ToString("yyyy-MM-ddTHH:mm:ss");
        var datosOrden = new { clienteId, vehiculoId, tipoServicio = "Frenos", descripcionProblema = "Ruido metálico al frenar (prueba automática)", fechaEstimadaFinalizacion = estimada };
        var orden = await Enviar(recepcion, HttpMethod.Post, "api/ordenes", datosOrden);
        Verificar("2. Crear una orden de reparación (Recibido)", orden.Status == 201 && Texto(orden.Json, "estado") == "Recibido", Detalle(orden));
        if (orden.Status != 201) return;
        var ordenId = Int(orden.Json!["id"]);
        var duplicada = await Enviar(recepcion, HttpMethod.Post, "api/ordenes", datosOrden);
        Verificar("Orden duplicada (mismo vehículo y servicio) -> 409", duplicada.Status == 409, Detalle(duplicada));

        // 3. Asignar mecánico
        var mecanicos = await Enviar(supervisor, HttpMethod.Get, "api/mecanicos?soloActivos=true");
        var mecanicoId = Int(mecanicos.Json!.AsArray()[0]!["id"]);
        var (inicio, fin) = HorarioLibre(0);
        var asignacion = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/asignar", new { mecanicoId, inicio, fin });
        Verificar("3. Asignar un mecánico", asignacion.Status == 200, Detalle(asignacion));

        // 4. Modificar sin permiso -> 403 (rol correcto, sin claim) y cliente -> 403
        var sinClaim = await Enviar(limitada, HttpMethod.Put, $"api/ordenes/{ordenId}",
            new { descripcionProblema = "Intento de edición sin el permiso Ordenes.Editar", fechaEstimadaFinalizacion = estimada });
        Verificar("4. Modificar una orden sin permisos -> 403 (claim)", sinClaim.Status == 403, Detalle(sinClaim));
        var clienteEdita = await Enviar(cliente, HttpMethod.Patch, $"api/ordenes/{ordenId}/estado", new { nuevoEstado = "Cancelado", comentario = "prueba" });
        Verificar("4b. Un cliente intenta cambiar el estado -> 403 (rol)", clienteEdita.Status == 403, Detalle(clienteEdita));

        // Preparación del presupuesto: diagnóstico
        var diag1 = await Enviar(supervisor, HttpMethod.Patch, $"api/ordenes/{ordenId}/estado", new { nuevoEstado = "EnDiagnostico", comentario = "Inicia revisión" });
        var diag2 = await Enviar(supervisor, HttpMethod.Put, $"api/ordenes/{ordenId}/diagnostico", new { diagnostico = "Pastillas delanteras desgastadas al 90 por ciento." });
        Verificar("Estado En diagnóstico y diagnóstico registrado", diag1.Status == 200 && diag2.Status == 200, $"{Detalle(diag1)} / {Detalle(diag2)}");

        var repuesto = await AsegurarExistencias(admin, "FIL-ACE-001", 10);
        var repuestoId = Int(repuesto["id"]);
        var precio = repuesto["precio"]!.GetValue<decimal>();

        // 5. Presupuesto con subtotales y total
        var presupuesto = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/presupuesto",
            new { manoObra = 40.00m, observaciones = "Prueba automática", detalles = new[] { new { repuestoId, cantidad = 2 } } });
        var totalEsperado = 40.00m + precio * 2;
        var total = presupuesto.Json?["total"]?.GetValue<decimal>();
        Verificar("5. Crear un presupuesto (subtotal y total)", presupuesto.Status == 201 && total == totalEsperado,
            $"{Detalle(presupuesto)}; total {total} (esperado {totalEsperado})");
        if (presupuesto.Status != 201) return;
        var presupuestoId = Int(presupuesto.Json!["id"]);

        // 6. Aprobar con usuario autorizado
        var existenciasAntes = await Existencias(admin, repuestoId);
        var enviar = await Enviar(supervisor, HttpMethod.Post, $"api/presupuestos/{presupuestoId}/enviar", new { });
        var recepcionAprueba = await Enviar(recepcion, HttpMethod.Post, $"api/presupuestos/{presupuestoId}/aprobar", new { });
        Verificar("Aprobar con usuario sin permiso -> 403", recepcionAprueba.Status == 403, Detalle(recepcionAprueba));
        var aprobar = await Enviar(supervisor, HttpMethod.Post, $"api/presupuestos/{presupuestoId}/aprobar", new { comentario = "Aprobado en prueba" });
        var existenciasDespues = await Existencias(admin, repuestoId);
        Verificar("6. Aprobar el presupuesto (estado Aprobado)",
            enviar.Status == 200 && aprobar.Status == 200 && Texto(aprobar.Json, "estado") == "Aprobado" && Texto(aprobar.Json, "estadoOrden") == "Aprobado",
            Detalle(aprobar));
        Verificar("Al aprobar se descuenta el inventario automáticamente", existenciasDespues == existenciasAntes - 2,
            $"existencias {existenciasAntes} -> {existenciasDespues}");

        // 7. Fotografías
        var foto = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/evidencias",
            Multipart(Convert.FromBase64String(PngValido), "dano.png", "image/png", "Dano", "Rayón en puerta (prueba)"));
        var ruta = Texto(foto.Json, "ruta");
        var archivo = ruta is null ? null : await Enviar(anonimo, HttpMethod.Get, ruta);
        Verificar("7. Registrar fotografía (ruta guardada y archivo servido)",
            foto.Status == 201 && ruta?.StartsWith("uploads/") == true && archivo?.Status == 200, $"{Detalle(foto)}; ruta {ruta}");
        var txt = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/evidencias",
            Multipart(Encoding.UTF8.GetBytes("no es imagen"), "nota.txt", "text/plain", "Dano", null));
        Verificar("Imagen con formato inválido (.txt) -> 400", txt.Status == 400, Detalle(txt));
        var falsa = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/evidencias",
            Multipart(Encoding.UTF8.GetBytes("contenido que no es PNG"), "falsa.png", "image/png", "Dano", null));
        Verificar("Archivo .png con contenido falso -> 400", falsa.Status == 400, Detalle(falsa));
        var grande = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/evidencias",
            Multipart(ImagenGrande(), "grande.png", "image/png", "Dano", null));
        Verificar("Imagen mayor a 5 MB -> rechazada (400/413)", grande.Status is 400 or 413, Detalle(grande));

        // 8. Descontar repuestos
        var antesSalida = await Existencias(admin, repuestoId);
        var salida = await Enviar(supervisor, HttpMethod.Post, $"api/repuestos/{repuestoId}/salidas", new { cantidad = 1, motivo = "Prueba de salida" });
        Verificar("8. Descontar repuestos (existencias + movimiento)",
            salida.Status == 200 && Int(salida.Json!["repuesto"]!["existencias"]) == antesSalida - 1 && Texto(salida.Json["movimiento"], "tipo") == "Salida",
            Detalle(salida));

        // 9. Descontar más de lo disponible
        var antesExceso = await Existencias(admin, repuestoId);
        var exceso = await Enviar(supervisor, HttpMethod.Post, $"api/repuestos/{repuestoId}/salidas", new { cantidad = antesExceso + 1, motivo = "Prueba de exceso" });
        var despuesExceso = await Existencias(admin, repuestoId);
        Verificar("9. Descontar más de lo disponible -> error y sin cambios",
            exceso.Status == 400 && antesExceso == despuesExceso, $"{Detalle(exceso)}; existencias {antesExceso} -> {despuesExceso}");

        // Avance hasta Entregado (flujo completo)
        var reparar = await Enviar(supervisor, HttpMethod.Patch, $"api/ordenes/{ordenId}/estado", new { nuevoEstado = "EnReparacion" });
        var finalizar = await Enviar(supervisor, HttpMethod.Post, $"api/ordenes/{ordenId}/finalizar", new { comentario = "Listo" });
        var entregar = await Enviar(recepcion, HttpMethod.Patch, $"api/ordenes/{ordenId}/estado", new { nuevoEstado = "Entregado" });
        Verificar("Flujo completo: En reparación -> Finalizado -> Entregado",
            reparar.Status == 200 && finalizar.Status == 200 && entregar.Status == 200, $"{reparar.Status}/{finalizar.Status}/{entregar.Status}");
        var saltoInvalido = await Enviar(supervisor, HttpMethod.Patch, $"api/ordenes/{ordenId}/estado", new { nuevoEstado = "EnDiagnostico" });
        Verificar("Saltar pasos de estado -> 400", saltoInvalido.Status == 400, Detalle(saltoInvalido));

        // El cliente consulta su propia orden (cliente sembrado) pero no la de otro
        var ajena = await Enviar(cliente, HttpMethod.Get, $"api/ordenes/{ordenId}");
        Verificar("Cliente no puede ver órdenes ajenas -> 403", ajena.Status == 403, Detalle(ajena));

        // 12. Logs de una operación fallida
        var log = await BuscarLog(admin, sinClaim.CorrelationId);
        Verificar("12. Log de operación fallida (usuario, endpoint, código y Correlation ID)",
            log is not null && Int(log["codigoEstado"]) == 403 && Texto(log, "usuario") == "recepcion.limitada@autofix.com"
            && Texto(log, "endpoint")?.Contains("/api/ordenes/") == true,
            log is null ? "no se encontró el log" : $"{Texto(log, "metodo")} {Texto(log, "endpoint")} -> {log["codigoEstado"]} | {Texto(log, "correlationId")}");
    }

    // =====================================================================
    // Prueba 10: concurrencia (escenarios A y B) con Task.WhenAll
    // =====================================================================
    public async Task ConcurrenciaAsync()
    {
        Titulo("10. CONCURRENCIA");
        var admin = await Sesion("admin@autofix.com", "Admin#2026");
        var supervisor1 = await Sesion("supervisor@autofix.com", "Supervisor#2026");
        var supervisor2 = await Sesion("supervisor2@autofix.com", "Supervisor#2026");
        var recepcion = await Sesion("recepcion@autofix.com", "Recepcion#2026");

        // Escenario A: 2 unidades, dos usuarios piden 2 cada uno al mismo tiempo.
        var creado = await Enviar(admin, HttpMethod.Post, "api/repuestos", new
        {
            codigo = $"CONC-{_sufijo}", nombre = "Repuesto para prueba de concurrencia", descripcion = "Creado por AutoFix.Pruebas",
            categoria = "Pruebas", marca = "AutoFix", precio = 10.00m, existencias = 2, stockMinimo = 0
        });
        if (creado.Status != 201) { Verificar("Escenario A: preparación", false, Detalle(creado)); return; }
        var repuestoId = Int(creado.Json!["id"]);
        var version = Texto(creado.Json, "version");

        var tareasA = new[]
        {
            Enviar(supervisor1, HttpMethod.Post, $"api/repuestos/{repuestoId}/salidas", new { cantidad = 2, motivo = "Reparación A", version }, $"CONC-A1-{_sufijo}"),
            Enviar(supervisor2, HttpMethod.Post, $"api/repuestos/{repuestoId}/salidas", new { cantidad = 2, motivo = "Reparación B", version }, $"CONC-A2-{_sufijo}")
        };
        var a = await Task.WhenAll(tareasA);
        var finales = await Existencias(admin, repuestoId);
        Console.WriteLine($"   Petición A1 -> {Detalle(a[0])} | Correlation ID {a[0].CorrelationId}");
        Console.WriteLine($"   Petición A2 -> {Detalle(a[1])} | Correlation ID {a[1].CorrelationId}");
        Verificar("Escenario A: solo una salida se aplica; la otra recibe 409",
            a.Count(r => r.Status == 200) == 1 && a.Count(r => r.Status == 409) == 1 && finales == 0,
            $"códigos {a[0].Status} y {a[1].Status}; existencias finales {finales} (nunca negativas)");

        // Escenario B: dos supervisores asignan al mismo mecánico en horarios que se cruzan.
        var nuevoCliente = await Enviar(recepcion, HttpMethod.Post, "api/clientes", ClienteDePrueba("CC"));
        if (nuevoCliente.Status != 201) { Verificar("Escenario B: preparación", false, Detalle(nuevoCliente)); return; }
        var clienteId = Int(nuevoCliente.Json!["cliente"]!["id"]);
        var vehiculos = nuevoCliente.Json["vehiculos"]!.AsArray();
        var estimada = DateTime.Now.AddDays(5).ToString("yyyy-MM-ddTHH:mm:ss");
        var ordenA = await Enviar(recepcion, HttpMethod.Post, "api/ordenes",
            new { clienteId, vehiculoId = Int(vehiculos[0]!["id"]), tipoServicio = "Motor", descripcionProblema = "Prueba de concurrencia, orden A", fechaEstimadaFinalizacion = estimada });
        var ordenB = await Enviar(recepcion, HttpMethod.Post, "api/ordenes",
            new { clienteId, vehiculoId = Int(vehiculos[1]!["id"]), tipoServicio = "Motor", descripcionProblema = "Prueba de concurrencia, orden B", fechaEstimadaFinalizacion = estimada });
        if (ordenA.Status != 201 || ordenB.Status != 201) { Verificar("Escenario B: preparación", false, $"{Detalle(ordenA)} / {Detalle(ordenB)}"); return; }

        var mecanicos = await Enviar(admin, HttpMethod.Get, "api/mecanicos?soloActivos=true");
        var mecanico = mecanicos.Json!.AsArray()[0]!;
        var mecanicoId = Int(mecanico["id"]);
        var versionMecanico = Texto(mecanico, "version");
        var (inicio, fin) = HorarioLibre(0);
        var (inicioB, finB) = HorarioLibre(1, inicio); // se cruza una hora con el primero

        var tareasB = new[]
        {
            Enviar(supervisor1, HttpMethod.Post, $"api/ordenes/{Int(ordenA.Json!["id"])}/asignar", new { mecanicoId, inicio, fin, versionMecanico }, $"CONC-B1-{_sufijo}"),
            Enviar(supervisor2, HttpMethod.Post, $"api/ordenes/{Int(ordenB.Json!["id"])}/asignar", new { mecanicoId, inicio = inicioB, fin = finB, versionMecanico }, $"CONC-B2-{_sufijo}")
        };
        var b = await Task.WhenAll(tareasB);
        Console.WriteLine($"   Petición B1 -> {Detalle(b[0])} | Correlation ID {b[0].CorrelationId}");
        Console.WriteLine($"   Petición B2 -> {Detalle(b[1])} | Correlation ID {b[1].CorrelationId}");
        Verificar("Escenario B: solo una asignación se aplica; la otra recibe 409",
            b.Count(r => r.Status == 200) == 1 && b.Count(r => r.Status == 409) == 1,
            $"códigos {b[0].Status} y {b[1].Status}");
    }

    public int Resumen()
    {
        Titulo("RESUMEN");
        var fallas = _resultados.Count(r => !r.Ok);
        Console.WriteLine($"{_resultados.Count - fallas} de {_resultados.Count} verificaciones correctas.");
        foreach (var f in _resultados.Where(r => !r.Ok)) Console.WriteLine($"  FALLÓ: {f.Nombre} ({f.Detalle})");
        return fallas == 0 ? 0 : 1;
    }

    // ---------------- Auxiliares ----------------

    private object ClienteDePrueba(string prefijo) => new
    {
        nombreCompleto = $"Cliente de prueba {prefijo}{_sufijo}",
        dui = $"{prefijo}-{_sufijo}",
        telefono = "7000-1234",
        correo = $"{prefijo.ToLowerInvariant()}{_sufijo}@prueba.com",
        direccion = "Dirección de prueba 123",
        vehiculos = new[]
        {
            new { placa = $"{prefijo}{_sufijo}A", marca = "Toyota", modelo = "Yaris", anio = 2020, color = "Rojo", numeroChasis = $"{prefijo}CH{_sufijo}A" },
            new { placa = $"{prefijo}{_sufijo}B", marca = "Kia", modelo = "Rio", anio = 2022, color = "Gris", numeroChasis = $"{prefijo}CH{_sufijo}B" }
        }
    };

    /// <summary>Horario lejano y aleatorio (evita choques entre ejecuciones). desplazamiento=1 se cruza con el base.</summary>
    private (string Inicio, string Fin) HorarioLibre(int desplazamientoHoras, string? baseInicio = null)
    {
        var inicio = baseInicio is null
            ? DateTime.Today.AddDays(Random.Shared.Next(60, 3000)).AddHours(Random.Shared.Next(7, 14))
            : DateTime.Parse(baseInicio).AddHours(desplazamientoHoras);
        return (inicio.ToString("yyyy-MM-ddTHH:mm:ss"), inicio.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss"));
    }

    private async Task<JsonNode> AsegurarExistencias(HttpClient admin, string codigo, int minimo)
    {
        var lista = await Enviar(admin, HttpMethod.Get, $"api/repuestos?buscar={codigo}");
        var repuesto = lista.Json!.AsArray().First(r => Texto(r, "codigo") == codigo)!;
        if (Int(repuesto["existencias"]) < minimo)
        {
            var entrada = await Enviar(admin, HttpMethod.Post, $"api/repuestos/{Int(repuesto["id"])}/entradas", new { cantidad = 20, motivo = "Reposición para pruebas" });
            repuesto = entrada.Json!["repuesto"]!;
        }
        return repuesto;
    }

    private async Task<int> Existencias(HttpClient c, int repuestoId) =>
        Int((await Enviar(c, HttpMethod.Get, $"api/repuestos/{repuestoId}")).Json!["existencias"]);

    private async Task<JsonNode?> BuscarLog(HttpClient admin, string correlationId)
    {
        for (var intento = 0; intento < 6; intento++)
        {
            var r = await Enviar(admin, HttpMethod.Get, $"api/logs?correlationId={correlationId}");
            var log = r.Json?.AsArray().FirstOrDefault();
            if (log is not null) return log;
            await Task.Delay(400);
        }
        return null;
    }

    private static byte[] ImagenGrande()
    {
        var datos = new byte[5 * 1024 * 1024 + 1024];
        Convert.FromBase64String(PngValido).CopyTo(datos, 0);
        return datos;
    }

    private static MultipartFormDataContent Multipart(byte[] bytes, string nombre, string tipoContenido, string tipo, string? descripcion)
    {
        var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(bytes);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipoContenido);
        contenido.Add(archivo, "Archivo", nombre);
        contenido.Add(new StringContent(tipo), "Tipo");
        if (descripcion is not null) contenido.Add(new StringContent(descripcion), "Descripcion");
        return contenido;
    }

    private HttpClient NuevoCliente()
    {
        // Solo para pruebas locales: acepta el certificado de desarrollo aunque no esté marcado como confiable.
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        return new HttpClient(handler) { BaseAddress = new Uri(_base), Timeout = TimeSpan.FromSeconds(90) };
    }

    private async Task<HttpClient> Sesion(string email, string password)
    {
        if (_sesiones.TryGetValue(email, out var existente)) return existente;
        var cliente = NuevoCliente();
        var r = await Enviar(cliente, HttpMethod.Post, "api/auth/login", new { email, password });
        if (r.Status != 200) throw new InvalidOperationException($"No se pudo iniciar sesión con {email}: {Detalle(r)}");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Texto(r.Json, "token"));
        _sesiones[email] = cliente;
        return cliente;
    }

    private static async Task<Resp> Enviar(HttpClient c, HttpMethod metodo, string url, object? cuerpo = null, string? correlationId = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, url);
        if (cuerpo is HttpContent contenido) solicitud.Content = contenido;
        else if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo);
        var cid = correlationId ?? $"PRUEBA-{Guid.NewGuid():N}";
        solicitud.Headers.Add("X-Correlation-ID", cid);

        using var respuesta = await c.SendAsync(solicitud);
        var texto = await respuesta.Content.ReadAsStringAsync();
        JsonNode? json = null;
        var tipo = respuesta.Content.Headers.ContentType?.MediaType ?? "";
        if (tipo.Contains("json") && !string.IsNullOrWhiteSpace(texto))
        {
            try { json = JsonNode.Parse(texto); } catch (Exception) { /* cuerpo no JSON */ }
        }
        return new Resp((int)respuesta.StatusCode, json, texto, cid);
    }

    private static int Int(JsonNode? nodo) => nodo?.GetValue<int>() ?? 0;
    private static string? Texto(JsonNode? nodo, string propiedad) => nodo?[propiedad]?.GetValue<string>();

    private static string Detalle(Resp r)
    {
        var mensaje = r.Json is JsonObject o && o["mensaje"] is JsonNode m ? $": {m.GetValue<string>()}" : "";
        return $"HTTP {r.Status}{mensaje}";
    }

    private void Verificar(string nombre, bool ok, string detalle)
    {
        _resultados.Add((nombre, ok, detalle));
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write(ok ? "[ OK ] " : "[FALLA] ");
        Console.ForegroundColor = color;
        Console.WriteLine($"{nombre}  ->  {detalle}");
    }

    private static void Titulo(string texto)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine(texto);
        Console.WriteLine(new string('=', 70));
    }
}
