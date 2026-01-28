using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ProductRecommender.Backend.Models.Core;

public partial class UpgradedbContext : DbContext
{
    public UpgradedbContext()
    {
    }

    public UpgradedbContext(DbContextOptions<UpgradedbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<NotasPedidoCab> NotasPedidoCabs { get; set; }

    public virtual DbSet<NotasPedidoDet> NotasPedidoDets { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => base.OnConfiguring(optionsBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("dblink");

        modelBuilder.Entity<NotasPedidoCab>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notas_pedido_cab_pk");

            entity.ToTable("notas_pedido_cab", "cmrlz");

            entity.HasIndex(e => new { e.AlmacenId, e.Numero }, "notas_pedido_uk").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AlmacenEntregaId).HasColumnName("almacen_entrega_id");
            entity.Property(e => e.AlmacenId).HasColumnName("almacen_id");
            entity.Property(e => e.Anulada)
                .HasDefaultValue(false)
                .HasColumnName("anulada");
            entity.Property(e => e.AprobacionComentario).HasColumnName("aprobacion_comentario");
            entity.Property(e => e.Aprobada)
                .HasDefaultValue(false)
                .HasColumnName("aprobada");
            entity.Property(e => e.AprobadoCredito)
                .HasDefaultValue(false)
                .HasColumnName("aprobado_credito");
            entity.Property(e => e.AprobadoCreditoAprobador).HasColumnName("aprobado_credito_aprobador");
            entity.Property(e => e.AprobadoCreditoComentario).HasColumnName("aprobado_credito_comentario");
            entity.Property(e => e.AprobadoEn)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("aprobado_en");
            entity.Property(e => e.AprobadoSinAdelanto)
                .HasDefaultValue(false)
                .HasColumnName("aprobado_sin_adelanto");
            entity.Property(e => e.AprobadoSinAdelantoPorId).HasColumnName("aprobado_sin_adelanto_por_id");
            entity.Property(e => e.AprobadorUtilidad).HasColumnName("aprobador_utilidad");
            entity.Property(e => e.Categoria)
                .HasMaxLength(1)
                .HasColumnName("categoria");
            entity.Property(e => e.Cerrada)
                .HasDefaultValue(false)
                .HasColumnName("cerrada");
            entity.Property(e => e.Comision)
                .HasDefaultValue(false)
                .HasColumnName("comision");
            entity.Property(e => e.ComisionMontoUsado)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0.00")
                .HasColumnName("comision_monto_usado");
            entity.Property(e => e.ComisionTerceros)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0.00")
                .HasColumnName("comision_terceros");
            entity.Property(e => e.CotizacionId).HasColumnName("cotizacion_id");
            entity.Property(e => e.Creado)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("creado");
            entity.Property(e => e.CreadoIp)
                .HasDefaultValueSql("inet_client_addr()")
                .HasColumnName("creado_ip");
            entity.Property(e => e.CreadoPor)
                .HasDefaultValue(0)
                .HasColumnName("creado_por");
            entity.Property(e => e.CuponId).HasColumnName("cupon_id");
            entity.Property(e => e.DeComision)
                .HasDefaultValue(false)
                .HasColumnName("de_comision");
            entity.Property(e => e.Descargado)
                .HasDefaultValue(false)
                .HasColumnName("descargado");
            entity.Property(e => e.DescripcionGastos).HasColumnName("descripcion_gastos");
            entity.Property(e => e.DiasCredito)
                .HasDefaultValue(0)
                .HasColumnName("dias_credito");
            entity.Property(e => e.DireccionClienteId).HasColumnName("direccion_cliente_id");
            entity.Property(e => e.DireccionEcommerceId).HasColumnName("direccion_ecommerce_id");
            entity.Property(e => e.DocPagoId).HasColumnName("doc_pago_id");
            entity.Property(e => e.EditDetalles).HasColumnName("edit_detalles");
            entity.Property(e => e.Editado)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("editado");
            entity.Property(e => e.EditadoIp).HasColumnName("editado_ip");
            entity.Property(e => e.EditadoPor).HasColumnName("editado_por");
            entity.Property(e => e.EsComision)
                .HasDefaultValue(false)
                .HasColumnName("es_comision");
            entity.Property(e => e.EsGastoOperativo)
                .HasDefaultValue(false)
                .HasColumnName("es_gasto_operativo");
            entity.Property(e => e.EstadoActual)
                .HasMaxLength(255)
                .HasColumnName("estado_actual");
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.FechaAprobacionUtilidad).HasColumnName("fecha_aprobacion_utilidad");
            entity.Property(e => e.FechaEntrega)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("fecha_entrega");
            entity.Property(e => e.FormaPagoId).HasColumnName("forma_pago_id");
            entity.Property(e => e.GastosOperativos)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0.00")
                .HasColumnName("gastos_operativos");
            entity.Property(e => e.MonedaId).HasColumnName("moneda_id");
            entity.Property(e => e.MontoAdelanto)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("monto_adelanto");
            entity.Property(e => e.NotaPedidoComisionId).HasColumnName("nota_pedido_comision_id");
            entity.Property(e => e.Numero).HasColumnName("numero");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");
            entity.Property(e => e.OrdenCompraCliente).HasColumnName("orden_compra_cliente");
            entity.Property(e => e.PreventaTipoId).HasColumnName("preventa_tipo_id");
            entity.Property(e => e.ProformaId).HasColumnName("proforma_id");
            entity.Property(e => e.TipoEntrega)
                .HasMaxLength(255)
                .HasColumnName("tipo_entrega");
            entity.Property(e => e.TokenCargo)
                .HasMaxLength(256)
                .HasColumnName("token_cargo");
            entity.Property(e => e.TokenTarjeta)
                .HasMaxLength(256)
                .HasColumnName("token_tarjeta");
            entity.Property(e => e.Total)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("total");
            entity.Property(e => e.TotalMinimo)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0.00")
                .HasColumnName("total_minimo");
            entity.Property(e => e.TotalReal)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0.00")
                .HasColumnName("total_real");
            entity.Property(e => e.TotalSinComision)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0.00")
                .HasColumnName("total_sin_comision");
            entity.Property(e => e.UnidadNegocioId).HasColumnName("unidad_negocio_id");
            entity.Property(e => e.UtilidadMinima)
                .HasPrecision(8, 6)
                .HasDefaultValueSql("0.16")
                .HasColumnName("utilidad_minima");
            entity.Property(e => e.VendedorAtendedor).HasColumnName("vendedor_atendedor");
            entity.Property(e => e.VendedorId).HasColumnName("vendedor_id");
            entity.Property(e => e.VentaId).HasColumnName("venta_id");

            entity.HasOne(d => d.NotaPedidoComision).WithMany(p => p.InverseNotaPedidoComision)
                .HasForeignKey(d => d.NotaPedidoComisionId)
                .HasConstraintName("notas_pedido_cab_nota_pedido_comision_id_fkey1");
        });

        modelBuilder.Entity<NotasPedidoDet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notas_pedido_det_pk");

            entity.ToTable("notas_pedido_det", "cmrlz");

            entity.HasIndex(e => new { e.NotaPedidoId, e.ProductoId }, "notas_pedido_det_uk").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Cantidad)
                .HasPrecision(18, 2)
                .HasColumnName("cantidad");
            entity.Property(e => e.CantidadEntregada)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("cantidad_entregada");
            entity.Property(e => e.Creado)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("creado");
            entity.Property(e => e.CreadoIp)
                .HasDefaultValueSql("inet_client_addr()")
                .HasColumnName("creado_ip");
            entity.Property(e => e.CreadoPor)
                .HasDefaultValue(0)
                .HasColumnName("creado_por");
            entity.Property(e => e.Editado)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("editado");
            entity.Property(e => e.EditadoIp).HasColumnName("editado_ip");
            entity.Property(e => e.EditadoPor).HasColumnName("editado_por");
            entity.Property(e => e.EntregaCompleta)
                .HasDefaultValue(false)
                .HasColumnName("entrega_completa");
            entity.Property(e => e.Exonerado)
                .HasDefaultValue(false)
                .HasColumnName("exonerado");
            entity.Property(e => e.NotaPedidoId).HasColumnName("nota_pedido_id");
            entity.Property(e => e.PrecioUnitarioNota)
                .HasPrecision(18, 4)
                .HasDefaultValueSql("0.00")
                .HasColumnName("precio_unitario_nota");
            entity.Property(e => e.PrecioUnitarioProducto)
                .HasPrecision(18, 4)
                .HasColumnName("precio_unitario_producto");
            entity.Property(e => e.PrecioUnitarioVenta)
                .HasPrecision(18, 6)
                .HasColumnName("precio_unitario_venta");
            entity.Property(e => e.PrecioUnitarioVentaReal)
                .HasPrecision(18, 6)
                .HasColumnName("precio_unitario_venta_real");
            entity.Property(e => e.ProductoCambioId).HasColumnName("producto_cambio_id");
            entity.Property(e => e.ProductoId).HasColumnName("producto_id");
            entity.Property(e => e.Regalo)
                .HasDefaultValue(false)
                .HasColumnName("regalo");
            entity.Property(e => e.SolicitudCompraId).HasColumnName("solicitud_compra_id");

            entity.HasOne(d => d.NotaPedido).WithMany(p => p.NotasPedidoDets)
                .HasForeignKey(d => d.NotaPedidoId)
                .HasConstraintName("notas_pedido_det_fk_notas_pedido_cab");

            entity.HasOne(d => d.ProductoCambio).WithMany(p => p.NotasPedidoDetProductoCambios)
                .HasForeignKey(d => d.ProductoCambioId)
                .HasConstraintName("notas_pedido_det_fk_productos_cambio");

            entity.HasOne(d => d.Producto).WithMany(p => p.NotasPedidoDetProductos)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notas_pedido_det_fk_productos");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("productos_pk");

            entity.ToTable("productos", "extcs");

            entity.HasIndex(e => e.Codigo, "productos_codigo_uk").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CodSunat).HasColumnName("cod_sunat");
            entity.Property(e => e.Codigo).HasColumnName("codigo");
            entity.Property(e => e.Creado)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("creado");
            entity.Property(e => e.CreadoIp)
                .HasDefaultValueSql("inet_client_addr()")
                .HasColumnName("creado_ip");
            entity.Property(e => e.CreadoPor)
                .HasDefaultValue(0)
                .HasColumnName("creado_por");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Divisible)
                .HasDefaultValue(false)
                .HasColumnName("divisible");
            entity.Property(e => e.DivisibleCantidad)
                .HasPrecision(18, 2)
                .HasColumnName("divisible_cantidad");
            entity.Property(e => e.DivisibleUnidadId).HasColumnName("divisible_unidad_id");
            entity.Property(e => e.EcomImg1Nombre)
                .HasMaxLength(255)
                .HasColumnName("ecom_img1_nombre");
            entity.Property(e => e.EcomImg2Nombre)
                .HasMaxLength(255)
                .HasColumnName("ecom_img2_nombre");
            entity.Property(e => e.EcomImg3Nombre)
                .HasMaxLength(255)
                .HasColumnName("ecom_img3_nombre");
            entity.Property(e => e.EcomLimite).HasColumnName("ecom_limite");
            entity.Property(e => e.EcomPrecio)
                .HasPrecision(18, 2)
                .HasColumnName("ecom_precio");
            entity.Property(e => e.Ecommerce)
                .HasDefaultValue(false)
                .HasColumnName("ecommerce");
            entity.Property(e => e.EcommerceDescrip)
                .HasMaxLength(2000)
                .HasColumnName("ecommerce_descrip");
            entity.Property(e => e.EcommerceNombre)
                .HasMaxLength(255)
                .HasColumnName("ecommerce_nombre");
            entity.Property(e => e.Editado)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("editado");
            entity.Property(e => e.EditadoIp).HasColumnName("editado_ip");
            entity.Property(e => e.EditadoPor).HasColumnName("editado_por");
            entity.Property(e => e.Inactivo)
                .HasDefaultValue(false)
                .HasColumnName("inactivo");
            entity.Property(e => e.LineaEcomId).HasColumnName("linea_ecom_id");
            entity.Property(e => e.LineaId).HasColumnName("linea_id");
            entity.Property(e => e.MarcaEcomId).HasColumnName("marca_ecom_id");
            entity.Property(e => e.MarcaId).HasColumnName("marca_id");
            entity.Property(e => e.Modelo).HasColumnName("modelo");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.Nuevo)
                .HasDefaultValue(false)
                .HasColumnName("nuevo");
            entity.Property(e => e.Peso)
                .HasPrecision(10, 2)
                .HasColumnName("peso");
            entity.Property(e => e.ProductoOrigenId).HasColumnName("producto_origen_id");
            entity.Property(e => e.ProductoTipoId)
                .HasDefaultValue(0)
                .HasColumnName("producto_tipo_id");
            entity.Property(e => e.Promocion)
                .HasDefaultValue(false)
                .HasColumnName("promocion");
            entity.Property(e => e.Regalo)
                .HasDefaultValue(false)
                .HasColumnName("regalo");
            entity.Property(e => e.Rotativo)
                .HasDefaultValue(false)
                .HasColumnName("rotativo");
            entity.Property(e => e.Servicio)
                .HasDefaultValue(false)
                .HasColumnName("servicio");
            entity.Property(e => e.StockEcom).HasColumnName("stock_ecom");
            entity.Property(e => e.SublineaEcomId).HasColumnName("sublinea_ecom_id");
            entity.Property(e => e.UnidadId).HasColumnName("unidad_id");

            entity.HasOne(d => d.ProductoOrigen).WithMany(p => p.InverseProductoOrigen)
                .HasForeignKey(d => d.ProductoOrigenId)
                .HasConstraintName("productos_fk_productos");
        });
        modelBuilder.HasSequence("actividades_vendedor_id_seq", "actvd");
        modelBuilder.HasSequence("banners2_id_seq", "ecommerce");
        modelBuilder.HasSequence("bannertable_id_seq", "ecommerce")
            .HasMax(99999L)
            .IsCyclic();
        modelBuilder.HasSequence("caracteristica2_id_seq", "ecommerce");
        modelBuilder.HasSequence("carrito_compras_det_id_seq", "ecommerce");
        modelBuilder.HasSequence("carrito_compras_id_seq", "ecommerce");
        modelBuilder.HasSequence("comisiones_id_seq");
        modelBuilder.HasSequence("compromisos_id_seq", "actvd");
        modelBuilder.HasSequence("escala_remunerativa_id_seq");
        modelBuilder.HasSequence("especificaciones_cab_id_seq", "ecommerce");
        modelBuilder.HasSequence("especificaciones_det_id_seq", "ecommerce");
        modelBuilder.HasSequence("ficha_actividad_id_seq", "actvd");
        modelBuilder.HasSequence("linea_ecom_caracteristica_id_seq", "ecommerce");
        modelBuilder.HasSequence("marca_ecom_id_seq", "ecommerce");
        modelBuilder.HasSequence("p_banner_img_id_seq", "ecommerce");
        modelBuilder.HasSequence("p_producto_img_id_seq", "ecommerce");
        modelBuilder.HasSequence("patrimonio_id_seq", "patrimonio");
        modelBuilder.HasSequence("patrimonio2_id_seq", "patrimonio");
        modelBuilder.HasSequence("programbanner_id_seq", "ecommerce")
            .HasMax(99999L)
            .IsCyclic();
        modelBuilder.HasSequence("rol_web_id_seq", "ecommerce");
        modelBuilder.HasSequence("seguimiento_coorporativo_id_seq", "actvd");
        modelBuilder.HasSequence("seguimiento_gobierno_id_seq", "actvd");
        modelBuilder.HasSequence("sliders2_id_seq", "ecommerce");
        modelBuilder.HasSequence("sublinea_ecommerce_id_seq", "ecommerce");
        modelBuilder.HasSequence("tipo_actividad_id_seq");
        modelBuilder.HasSequence("usuario_web2_id_seq", "ecommerce");
        modelBuilder.HasSequence("usuarios_almacenes_id_seq", "usros");
        modelBuilder.HasSequence("usuarios_cajas_id_seq", "usros");
        modelBuilder.HasSequence("usuarios_id_seq_temporal", "usros")
            .StartsAt(27000L)
            .HasMax(2147483648L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
