namespace AutoFix.Api.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Dui { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Direccion { get; set; } = "";
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public List<Vehiculo> Vehiculos { get; set; } = new();
    public List<OrdenReparacion> Ordenes { get; set; } = new();
}

public class Vehiculo
{
    public int Id { get; set; }
    public string Placa { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
    public int Anio { get; set; }
    public string Color { get; set; } = "";
    public string NumeroChasis { get; set; } = "";
    public string? FotoRuta { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public List<OrdenReparacion> Ordenes { get; set; } = new();
}
