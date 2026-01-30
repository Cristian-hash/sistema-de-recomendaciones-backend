using System;
using System.Collections.Generic;

namespace ProductRecommender.Backend.Models.Core;

public partial class Almacen
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public bool? Activo { get; set; }

    public virtual ICollection<ProductoAlmacen> ProductoAlmacens { get; set; } = new List<ProductoAlmacen>();
}
