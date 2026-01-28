using Microsoft.EntityFrameworkCore;

namespace ProductRecommender.Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Aquí agregaremos los DbSets según las tablas de la imagen
        // Ejemplo: public DbSet<NotaPedido> NotasPedido { get; set; }
    }
}
