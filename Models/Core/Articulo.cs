using System;
using System.Collections.Generic;

namespace ProductRecommender.Backend.Models.Core;

public partial class Articulo
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int? AlmacenId { get; set; }
    public bool Inactivo { get; set; }
    public string? Serie { get; set; }
    public string? Estado { get; set; } 

    public virtual Producto Producto { get; set; } = null!;
    public virtual Almacen? Almacen { get; set; }
}
