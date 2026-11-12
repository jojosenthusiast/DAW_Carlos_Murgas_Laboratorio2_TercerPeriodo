using AutoFix.Api.DTOs;
using AutoFix.Api.Entities;

namespace AutoFix.Api.Services;

public static class Mapeos
{
    public static UsuarioDto ToDto(this Usuario u) => new(
        u.Id, u.NombreCompleto, u.Email, u.Rol,
        u.Permisos.Select(p => p.Permiso).OrderBy(p => p).ToList(),
        u.ClienteId, u.MecanicoId, u.Activo, u.UltimoAcceso);

    public static VehiculoDto ToDto(this Vehiculo v) => new(
        v.Id, v.Placa, v.Marca, v.Modelo, v.Anio, v.Color, v.NumeroChasis,
        v.ClienteId, v.Cliente?.NombreCompleto ?? "", v.FotoRuta, v.FechaRegistro);

    public static RepuestoDto ToDto(this Repuesto r) => new(
        r.Id, r.Codigo, r.Nombre, r.Descripcion, r.Categoria, r.Marca, r.Precio,
        r.Existencias, r.StockMinimo, r.FotoRuta, r.Activo, r.Existencias <= r.StockMinimo, r.Version);

    public static MovimientoDto ToDto(this MovimientoInventario m) => new(
        m.Id, m.RepuestoId, m.Repuesto?.Codigo ?? "", m.Repuesto?.Nombre ?? "", m.Tipo, m.Cantidad,
        m.ExistenciaAnterior, m.ExistenciaNueva, m.Fecha, m.Usuario, m.Motivo, m.OrdenId, m.Orden?.NumeroOrden);

    public static AsignacionDto ToDto(this AsignacionMecanico a, string? numeroOrden = null) => new(
        a.Id, a.MecanicoId, a.Mecanico?.NombreCompleto ?? "", a.OrdenId,
        numeroOrden ?? a.Orden?.NumeroOrden ?? "", a.Inicio, a.Fin, a.Activa, a.AsignadoPor, a.FechaRegistro);

    public static EvidenciaDto ToDto(this Evidencia e, string? numeroOrden = null) => new(
        e.Id, e.OrdenId, numeroOrden ?? e.Orden?.NumeroOrden ?? "", e.Tipo, e.Ruta, e.NombreOriginal,
        e.TamanoBytes, e.Descripcion, e.FechaSubida, e.SubidoPor);

    public static HistorialDto ToDto(this HistorialEstadoOrden h) =>
        new(h.EstadoAnterior, h.EstadoNuevo, h.Fecha, h.Usuario, h.Comentario);

    public static OrdenResumenDto ToResumen(this OrdenReparacion o) => new(
        o.Id, o.NumeroOrden, o.Cliente?.NombreCompleto ?? "", o.Vehiculo?.Placa ?? "",
        o.Vehiculo is null ? "" : $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Anio}",
        o.TipoServicio, o.Estado, o.FechaIngreso, o.FechaEstimadaFinalizacion, o.Mecanico?.NombreCompleto);
}
