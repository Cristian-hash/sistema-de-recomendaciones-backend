using System;
using System.Collections.Generic;
using System.Net;

namespace ProductRecommender.Backend.Models.Core;

public partial class Producto
{
    public DateTime Creado { get; set; }

    public int CreadoPor { get; set; }

    public IPAddress? CreadoIp { get; set; }

    public DateTime? Editado { get; set; }

    public int? EditadoPor { get; set; }

    public IPAddress? EditadoIp { get; set; }

    public int Id { get; set; }

    public bool Inactivo { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Regalo { get; set; }

    public int ProductoTipoId { get; set; }

    public int UnidadId { get; set; }

    public int MarcaId { get; set; }

    public int LineaId { get; set; }

    public string? Modelo { get; set; }

    public bool Servicio { get; set; }

    public decimal Peso { get; set; }

    public bool Divisible { get; set; }

    public int? DivisibleUnidadId { get; set; }

    public decimal? DivisibleCantidad { get; set; }

    public int? ProductoOrigenId { get; set; }

    public bool Rotativo { get; set; }

    public string? CodSunat { get; set; }

    public bool? Ecommerce { get; set; }

    public decimal? EcomPrecio { get; set; }

    public string? EcommerceNombre { get; set; }

    public string? EcommerceDescrip { get; set; }

    public string? EcomImg1Nombre { get; set; }

    public string? EcomImg2Nombre { get; set; }

    public string? EcomImg3Nombre { get; set; }

    public int? EcomLimite { get; set; }

    public int? StockEcom { get; set; }

    public int? LineaEcomId { get; set; }

    public bool? Promocion { get; set; }

    public bool? Nuevo { get; set; }

    public int? MarcaEcomId { get; set; }

    public int? SublineaEcomId { get; set; }

    public virtual ICollection<Producto> InverseProductoOrigen { get; set; } = new List<Producto>();

    public virtual ICollection<NotasPedidoDet> NotasPedidoDetProductoCambios { get; set; } = new List<NotasPedidoDet>();

    public virtual ICollection<NotasPedidoDet> NotasPedidoDetProductos { get; set; } = new List<NotasPedidoDet>();

    public virtual Producto? ProductoOrigen { get; set; }
}
