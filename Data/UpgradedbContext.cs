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
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=179.43.97.177;Port=9090;Database=upgradedb;Username=postgres;Password=rednavedb2015");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("dblink");

        modelBuilder.Entity<NotasPedidoCab>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notas_pedido_cab_pk");
            entity.ToTable("notas_pedido_cab", "cmrlz");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.DireccionClienteId).HasColumnName("direccion_cliente_id");
            entity.Property(e => e.AlmacenId).HasColumnName("almacen_id");
            entity.Property(e => e.Numero).HasColumnName("numero");
            entity.Property(e => e.Anulada).HasColumnName("anulada");
            entity.Property(e => e.Cerrada).HasColumnName("cerrada");
            entity.Property(e => e.Aprobada).HasColumnName("aprobada");
        });

        modelBuilder.Entity<NotasPedidoDet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notas_pedido_det_pk");
            entity.ToTable("notas_pedido_det", "cmrlz");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.NotaPedidoId).HasColumnName("nota_pedido_id");
            entity.Property(e => e.ProductoId).HasColumnName("producto_id");
            
            entity.HasOne(d => d.NotaPedido).WithMany(p => p.NotasPedidoDets)
                .HasForeignKey(d => d.NotaPedidoId)
                .HasConstraintName("notas_pedido_det_fk_notas_pedido_cab");

            entity.HasOne(d => d.Producto).WithMany(p => p.NotasPedidoDetProductos)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notas_pedido_det_fk_productos");

            entity.HasOne(d => d.ProductoCambio).WithMany(p => p.NotasPedidoDetProductoCambios)
                .HasForeignKey(d => d.ProductoCambioId)
                .HasConstraintName("notas_pedido_det_fk_productos_cambio");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("productos_pk");
            entity.ToTable("productos", "extcs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo).HasColumnName("codigo");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.EcomPrecio).HasColumnName("ecom_precio");
            entity.Property(e => e.Inactivo).HasColumnName("inactivo");
            entity.Property(e => e.Servicio).HasColumnName("servicio");
            
            // Mappings restored to fix "Column does not exist" errors
            entity.Property(e => e.CodSunat).HasColumnName("cod_sunat");
            entity.Property(e => e.MarcaId).HasColumnName("marca_id");
            entity.Property(e => e.LineaId).HasColumnName("linea_id");
            entity.Property(e => e.ProductoTipoId).HasColumnName("producto_tipo_id");
            entity.Property(e => e.UnidadId).HasColumnName("unidad_id");
            entity.Property(e => e.Modelo).HasColumnName("modelo");
            entity.Property(e => e.Peso).HasColumnName("peso");
            entity.Property(e => e.Divisible).HasColumnName("divisible");
            entity.Property(e => e.DivisibleUnidadId).HasColumnName("divisible_unidad_id");
            entity.Property(e => e.DivisibleCantidad).HasColumnName("divisible_cantidad");
            entity.Property(e => e.ProductoOrigenId).HasColumnName("producto_origen_id");
            entity.Property(e => e.Rotativo).HasColumnName("rotativo");
            entity.Property(e => e.Ecommerce).HasColumnName("ecommerce");
            entity.Property(e => e.EcommerceNombre).HasColumnName("ecommerce_nombre");
            entity.Property(e => e.EcommerceDescrip).HasColumnName("ecommerce_descrip");
            entity.Property(e => e.EcomImg1Nombre).HasColumnName("ecom_img1_nombre");
            entity.Property(e => e.EcomImg2Nombre).HasColumnName("ecom_img2_nombre");
            entity.Property(e => e.EcomImg3Nombre).HasColumnName("ecom_img3_nombre");
            entity.Property(e => e.EcomLimite).HasColumnName("ecom_limite");
            entity.Property(e => e.StockEcom).HasColumnName("stock_ecom");
            entity.Property(e => e.LineaEcomId).HasColumnName("linea_ecom_id");
            entity.Property(e => e.Promocion).HasColumnName("promocion");
            entity.Property(e => e.Nuevo).HasColumnName("nuevo");
            entity.Property(e => e.MarcaEcomId).HasColumnName("marca_ecom_id");
            entity.Property(e => e.SublineaEcomId).HasColumnName("sublinea_ecom_id");
            entity.Property(e => e.Regalo).HasColumnName("regalo");
            entity.Property(e => e.Creado).HasColumnName("creado");
            entity.Property(e => e.CreadoPor).HasColumnName("creado_por");
            entity.Property(e => e.CreadoIp).HasColumnName("creado_ip");
            entity.Property(e => e.Editado).HasColumnName("editado");
            entity.Property(e => e.EditadoPor).HasColumnName("editado_por");
            entity.Property(e => e.EditadoIp).HasColumnName("editado_ip");
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
