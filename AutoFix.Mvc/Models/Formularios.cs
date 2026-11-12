using System.ComponentModel.DataAnnotations;

namespace AutoFix.Mvc.Models;

// Formularios: las validaciones del navegador son una ayuda; la API vuelve a validar todo.

public class LoginVm
{
    [Required(ErrorMessage = "Escribe tu correo."), EmailAddress(ErrorMessage = "Correo inválido."), Display(Name = "Correo")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Escribe tu contraseña."), DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = "";
}

public class RegistroVm
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120, MinimumLength = 3), Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Correo inválido."), Display(Name = "Correo")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria."), DataType(DataType.Password), Display(Name = "Contraseña")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,64}$",
        ErrorMessage = "Mínimo 8 caracteres con mayúscula, minúscula, número y símbolo.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirma la contraseña."), DataType(DataType.Password), Display(Name = "Confirmar contraseña")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = "";

    [Required(ErrorMessage = "El DUI es obligatorio."), Display(Name = "DUI o identificación")]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,20}$", ErrorMessage = "Formato inválido. Ej.: 01234567-8")]
    public string Dui { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio."), Display(Name = "Teléfono")]
    [RegularExpression(@"^[0-9+()\-\s]{8,20}$", ErrorMessage = "Teléfono inválido.")]
    public string Telefono { get; set; } = "";

    [Required(ErrorMessage = "La dirección es obligatoria."), StringLength(250, MinimumLength = 5), Display(Name = "Dirección")]
    public string Direccion { get; set; } = "";
}

public class ClienteFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120, MinimumLength = 3), Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "El DUI es obligatorio."), Display(Name = "DUI o identificación")]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,20}$", ErrorMessage = "Formato inválido. Ej.: 01234567-8")]
    public string Dui { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio."), Display(Name = "Teléfono")]
    [RegularExpression(@"^[0-9+()\-\s]{8,20}$", ErrorMessage = "Teléfono inválido.")]
    public string Telefono { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Correo inválido."), Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "La dirección es obligatoria."), StringLength(250, MinimumLength = 5), Display(Name = "Dirección")]
    public string Direccion { get; set; } = "";
}

public class VehiculoFormVm
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el cliente propietario."), Display(Name = "Cliente propietario")]
    public int ClienteId { get; set; }

    [Required(ErrorMessage = "La placa es obligatoria."), Display(Name = "Placa")]
    [RegularExpression(@"^[A-Za-z0-9\- ]{3,12}$", ErrorMessage = "Placa inválida.")]
    public string Placa { get; set; } = "";

    [Required(ErrorMessage = "La marca es obligatoria."), StringLength(50), Display(Name = "Marca")]
    public string Marca { get; set; } = "";

    [Required(ErrorMessage = "El modelo es obligatorio."), StringLength(50), Display(Name = "Modelo")]
    public string Modelo { get; set; } = "";

    [Range(1950, 2100, ErrorMessage = "Año inválido."), Display(Name = "Año")]
    public int Anio { get; set; } = DateTime.Now.Year;

    [Required(ErrorMessage = "El color es obligatorio."), StringLength(30), Display(Name = "Color")]
    public string Color { get; set; } = "";

    [Required(ErrorMessage = "El número de chasis es obligatorio."), Display(Name = "Número de chasis")]
    [RegularExpression(@"^[A-Za-z0-9\-]{5,30}$", ErrorMessage = "Número de chasis inválido.")]
    public string NumeroChasis { get; set; } = "";
}

public class OrdenCrearVm
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el cliente."), Display(Name = "Cliente")]
    public int ClienteId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el vehículo."), Display(Name = "Vehículo")]
    public int VehiculoId { get; set; }

    [Required(ErrorMessage = "Selecciona el servicio."), Display(Name = "Tipo de servicio")]
    public string? TipoServicio { get; set; }

    [Required(ErrorMessage = "Describe el problema."), StringLength(1000, MinimumLength = 10, ErrorMessage = "Entre 10 y 1000 caracteres."), Display(Name = "Descripción del problema")]
    public string DescripcionProblema { get; set; } = "";

    [Required(ErrorMessage = "Indica la fecha estimada."), Display(Name = "Fecha estimada de finalización")]
    public DateTime? FechaEstimadaFinalizacion { get; set; } = DateTime.Today.AddDays(3).AddHours(17);
}

public class OrdenEditarVm
{
    public int Id { get; set; }
    public string NumeroOrden { get; set; } = "";
    public Guid Version { get; set; }

    [Required(ErrorMessage = "Describe el problema."), StringLength(1000, MinimumLength = 10, ErrorMessage = "Entre 10 y 1000 caracteres."), Display(Name = "Descripción del problema")]
    public string DescripcionProblema { get; set; } = "";

    [Required(ErrorMessage = "Indica la fecha estimada."), Display(Name = "Fecha estimada de finalización")]
    public DateTime? FechaEstimadaFinalizacion { get; set; }
}

public class PresupuestoFormVm
{
    public int? Id { get; set; }
    public int OrdenId { get; set; }
    public string NumeroOrden { get; set; } = "";
    public Guid? Version { get; set; }

    [Range(typeof(decimal), "0", "100000", ErrorMessage = "Monto inválido."), Display(Name = "Mano de obra ($)")]
    public decimal ManoObra { get; set; }

    [StringLength(1000), Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    public List<LineaPresupuestoVm> Detalles { get; set; } = new();

    public List<RepuestoVm> Repuestos { get; set; } = new();
}

public class LineaPresupuestoVm
{
    public int RepuestoId { get; set; }
    [Range(1, 1000, ErrorMessage = "Cantidad entre 1 y 1000.")]
    public int Cantidad { get; set; } = 1;
}

public class RepuestoFormVm
{
    public int? Id { get; set; }
    public Guid? Version { get; set; }

    [Required(ErrorMessage = "El código es obligatorio."), Display(Name = "Código")]
    [RegularExpression(@"^[A-Za-z0-9\-]{3,30}$", ErrorMessage = "Código inválido.")]
    public string Codigo { get; set; } = "";

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120, MinimumLength = 3), Display(Name = "Nombre")]
    public string Nombre { get; set; } = "";

    [StringLength(500), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria."), StringLength(60), Display(Name = "Categoría")]
    public string Categoria { get; set; } = "";

    [Required(ErrorMessage = "La marca es obligatoria."), StringLength(60), Display(Name = "Marca")]
    public string Marca { get; set; } = "";

    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Precio inválido."), Display(Name = "Precio ($)")]
    public decimal Precio { get; set; }

    [Range(0, 100000, ErrorMessage = "No puede ser negativo."), Display(Name = "Existencia inicial")]
    public int Existencias { get; set; }

    [Range(0, 100000, ErrorMessage = "No puede ser negativo."), Display(Name = "Stock mínimo")]
    public int StockMinimo { get; set; }
}

public class MecanicoFormVm
{
    public int? Id { get; set; }
    public Guid? Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120, MinimumLength = 3), Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "La especialidad es obligatoria."), StringLength(80, MinimumLength = 3), Display(Name = "Especialidad")]
    public string Especialidad { get; set; } = "";

    [Required(ErrorMessage = "El teléfono es obligatorio."), Display(Name = "Teléfono")]
    [RegularExpression(@"^[0-9+()\-\s]{8,20}$", ErrorMessage = "Teléfono inválido.")]
    public string Telefono { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Correo inválido."), Display(Name = "Correo")]
    public string Email { get; set; } = "";

    [DataType(DataType.Password), Display(Name = "Contraseña de acceso (opcional)")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,64}$",
        ErrorMessage = "Mínimo 8 caracteres con mayúscula, minúscula, número y símbolo.")]
    public string? PasswordUsuario { get; set; }
}

public class MovimientoFormVm
{
    public int RepuestoId { get; set; }
    public Guid? Version { get; set; }

    [Required] public string Tipo { get; set; } = "Entrada";

    [Range(1, 100000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int Cantidad { get; set; } = 1;

    [Required(ErrorMessage = "Indica el motivo."), StringLength(250, MinimumLength = 3)]
    public string Motivo { get; set; } = "";
}

public class EvidenciaFormVm
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona la orden."), Display(Name = "Orden")]
    public int OrdenId { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo."), Display(Name = "Tipo de evidencia")]
    public string Tipo { get; set; } = "Dano";

    [StringLength(300), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Display(Name = "Imagen (JPG o PNG, máx. 5 MB)")]
    public IFormFile? Archivo { get; set; }

    public List<OrdenResumenVm> Ordenes { get; set; } = new();
}

// ---------- Páginas compuestas ----------

public class OrdenDetallePaginaVm
{
    public OrdenDetalleVm Orden { get; set; } = default!;
    public PresupuestoVm? Presupuesto { get; set; }
    public List<MecanicoVm> Mecanicos { get; set; } = new();
    public List<RepuestoVm> Repuestos { get; set; } = new();
    public List<MovimientoVm> RepuestosUtilizados { get; set; } = new();
}

public class InventarioDetallePaginaVm
{
    public RepuestoVm Repuesto { get; set; } = default!;
    public List<MovimientoVm> Movimientos { get; set; } = new();
    public MovimientoFormVm Movimiento { get; set; } = new();
}

public class ReportesPaginaVm
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public ReporteOrdenesVm Ordenes { get; set; } = default!;
    public ReporteInventarioVm Inventario { get; set; } = default!;
    public List<CargaMecanicoVm> Mecanicos { get; set; } = new();
}
