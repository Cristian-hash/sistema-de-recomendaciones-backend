using System;
using System.Collections.Generic;

namespace ProductRecommender.Backend.Models.Core;

public partial class ProductoAlmacen
{
    public int ProductoId { get; set; }

    public int AlmacenId { get; set; }

    public decimal Stock { get; set; }

    public virtual Almacen Almacen { get; set; } = null!;

    public virtual Producto Producto { get; set; } = null!;
}
