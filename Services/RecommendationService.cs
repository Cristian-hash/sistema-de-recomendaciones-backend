using Microsoft.EntityFrameworkCore;
using ProductRecommender.Backend.Models.Core;

namespace ProductRecommender.Backend.Services
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public decimal? EcomPrecio { get; set; }
    }

    public interface IRecommendationService
    {
        Task<IEnumerable<ProductDto>> GetRecommendationsAsync(int productId, int limit = 5);
        Task<IEnumerable<ProductDto>> SearchProductsAsync(string term, int limit = 10);
        Task<IEnumerable<ProductDto>> GetSeasonalRecommendationsAsync(int month, int limit = 5);
        Task<IEnumerable<ProductDto>> GetClientRecommendationsAsync(int clientId, int limit = 5);
        Task<IEnumerable<int>> GetTopClientsAsync(int limit = 10);
    }

    public class RecommendationService : IRecommendationService
    {
        private readonly UpgradedbContext _context;

        public RecommendationService(UpgradedbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProductDto>> SearchProductsAsync(string term, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(term)) return new List<ProductDto>();

            return await _context.Productos
                .AsNoTracking()
                .Where(p => !p.Inactivo && (EF.Functions.ILike(p.Nombre, $"%{term}%") || EF.Functions.ILike(p.Codigo, $"%{term}%")))
                .Take(limit)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Codigo = p.Codigo,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    EcomPrecio = p.EcomPrecio
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductDto>> GetRecommendationsAsync(int productId, int limit = 5)
        {
            var orderIds = await _context.NotasPedidoDets
                .AsNoTracking()
                .Where(d => d.ProductoId == productId)
                .Select(d => d.NotaPedidoId)
                .Distinct()
                .Take(500)
                .ToListAsync();

            if (!orderIds.Any()) return new List<ProductDto>();

            var recommendations = await _context.NotasPedidoDets
                .AsNoTracking()
                .Where(d => orderIds.Contains(d.NotaPedidoId) && d.ProductoId != productId)
                .GroupBy(d => d.ProductoId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(limit)
                .ToListAsync();

            if (!recommendations.Any()) return new List<ProductDto>();

            return await GetProductsByIdsAsync(recommendations);
        }

        public async Task<IEnumerable<ProductDto>> GetSeasonalRecommendationsAsync(int month, int limit = 5)
        {
            var popularProductIds = await _context.NotasPedidoDets
                .AsNoTracking()
                .Include(d => d.NotaPedido)
                .Where(d => d.NotaPedido!.Fecha.Month == month)
                .GroupBy(d => d.ProductoId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(limit)
                .ToListAsync();

            return await GetProductsByIdsAsync(popularProductIds);
        }

        public async Task<IEnumerable<ProductDto>> GetClientRecommendationsAsync(int clientId, int limit = 5)
        {
            var frequentProductIds = await _context.NotasPedidoDets
                .AsNoTracking()
                .Include(d => d.NotaPedido)
                .Where(d => d.NotaPedido!.DireccionClienteId == clientId)
                .GroupBy(d => d.ProductoId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(limit)
                .ToListAsync();

            return await GetProductsByIdsAsync(frequentProductIds);
        }

        public async Task<IEnumerable<int>> GetTopClientsAsync(int limit = 10)
        {
            return await _context.NotasPedidoCabs
                .AsNoTracking()
                .GroupBy(c => c.DireccionClienteId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(limit)
                .ToListAsync();
        }

        private async Task<IEnumerable<ProductDto>> GetProductsByIdsAsync(List<int> productIds)
        {
            if (!productIds.Any()) return new List<ProductDto>();

            return await _context.Productos
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Codigo = p.Codigo,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    EcomPrecio = p.EcomPrecio
                })
                .ToListAsync();
        }
    }
}
