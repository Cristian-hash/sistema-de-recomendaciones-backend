using System;
using System.Collections.Generic;
using System.Net;

namespace ProductRecommender.Backend.Models.Core;

public partial class NotasPedidoDet
{
    public DateTime Creado { get; set; }

    public int CreadoPor { get; set; }

    public IPAddress CreadoIp { get; set; } = null!;

    public DateTime? Editado { get; set; }

    public int? EditadoPor { get; set; }

    public IPAddress? EditadoIp { get; set; }

    public int Id { get; set; }

    public int? NotaPedidoId { get; set; }

    public bool Exonerado { get; set; }

    public bool Regalo { get; set; }

    public int ProductoId { get; set; }

    public int? ProductoCambioId { get; set; }

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitarioProducto { get; set; }

    public decimal PrecioUnitarioNota { get; set; }

    public decimal? CantidadEntregada { get; set; }

    public bool EntregaCompleta { get; set; }

    public decimal? PrecioUnitarioVentaReal { get; set; }

    public decimal? PrecioUnitarioVenta { get; set; }

    public int? SolicitudCompraId { get; set; }

    public virtual NotasPedidoCab? NotaPedido { get; set; }

    public virtual Producto Producto { get; set; } = null!;

    public virtual Producto? ProductoCambio { get; set; }
}
