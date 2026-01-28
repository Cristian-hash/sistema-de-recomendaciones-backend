using System;
using System.Collections.Generic;
using System.Net;

namespace ProductRecommender.Backend.Models.Core;

public partial class NotasPedidoCab
{
    public DateTime Creado { get; set; }

    public int CreadoPor { get; set; }

    public IPAddress CreadoIp { get; set; } = null!;

    public DateTime? Editado { get; set; }

    public int? EditadoPor { get; set; }

    public IPAddress? EditadoIp { get; set; }

    public int Id { get; set; }

    public int Numero { get; set; }

    public DateOnly Fecha { get; set; }

    public bool Anulada { get; set; }

    public int DireccionClienteId { get; set; }

    public int VendedorId { get; set; }

    public int MonedaId { get; set; }

    public int PreventaTipoId { get; set; }

    public bool Cerrada { get; set; }

    public bool Aprobada { get; set; }

    public int FormaPagoId { get; set; }

    public int AlmacenId { get; set; }

    public DateTime FechaEntrega { get; set; }

    public string? Observaciones { get; set; }

    public int? ProformaId { get; set; }

    public int? VentaId { get; set; }

    public bool AprobadoSinAdelanto { get; set; }

    public int? AprobadoSinAdelantoPorId { get; set; }

    public decimal TotalMinimo { get; set; }

    public decimal? Total { get; set; }

    public decimal? MontoAdelanto { get; set; }

    public decimal UtilidadMinima { get; set; }

    public bool Comision { get; set; }

    public decimal ComisionMontoUsado { get; set; }

    public decimal? TotalSinComision { get; set; }

    public int DiasCredito { get; set; }

    public string? AprobacionComentario { get; set; }

    public int? AlmacenEntregaId { get; set; }

    public DateTime? AprobadoEn { get; set; }

    public decimal? TotalReal { get; set; }

    public bool? DeComision { get; set; }

    public bool? AprobadoCredito { get; set; }

    public string? AprobadoCreditoComentario { get; set; }

    public int? AprobadoCreditoAprobador { get; set; }

    public int? CotizacionId { get; set; }

    public int? AprobadorUtilidad { get; set; }

    public DateOnly? FechaAprobacionUtilidad { get; set; }

    public int? UnidadNegocioId { get; set; }

    public int? VendedorAtendedor { get; set; }

    public bool? Descargado { get; set; }

    public decimal? ComisionTerceros { get; set; }

    public char? Categoria { get; set; }

    public string? OrdenCompraCliente { get; set; }

    public int? EditDetalles { get; set; }

    public bool EsComision { get; set; }

    public int? NotaPedidoComisionId { get; set; }

    public decimal? GastosOperativos { get; set; }

    public bool? EsGastoOperativo { get; set; }

    public string? DescripcionGastos { get; set; }

    public string? EstadoActual { get; set; }

    public int? DireccionEcommerceId { get; set; }

    public string? TipoEntrega { get; set; }

    public int? CuponId { get; set; }

    public string? TokenTarjeta { get; set; }

    public string? TokenCargo { get; set; }

    public int? DocPagoId { get; set; }

    public virtual ICollection<NotasPedidoCab> InverseNotaPedidoComision { get; set; } = new List<NotasPedidoCab>();

    public virtual NotasPedidoCab? NotaPedidoComision { get; set; }

    public virtual ICollection<NotasPedidoDet> NotasPedidoDets { get; set; } = new List<NotasPedidoDet>();
}
