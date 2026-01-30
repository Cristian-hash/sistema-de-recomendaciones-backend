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
        public string? Razon { get; set; } // New field for the "Why"
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
        private readonly MLService _mlService;

        public RecommendationService(UpgradedbContext context, MLService mlService)
        {
            _context = context;
            _mlService = mlService;
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
            var product = await _context.Productos.FindAsync(productId);
            if (product == null) return new List<ProductDto>();

            var finalRecommendations = new List<ProductDto>();

            // 1. ESTRATEGIA DE "CASOS DE VENTA" (Las 7 Reglas)
            var strategyRecs = await GetStrategiesRecommendations(product);
            finalRecommendations.AddRange(strategyRecs);

            // Si ya llenamos el cupo, retornamos
            if (finalRecommendations.Count >= limit) 
                return finalRecommendations.Take(limit);

            // 2. ESTADÍSTICO (Relleno)
            try 
            {
                var currentIds = finalRecommendations.Select(r => r.Id).ToList();
                currentIds.Add(productId); // Excluir el mismo producto

                var orderIds = await _context.NotasPedidoDets
                    .AsNoTracking()
                    .Where(d => d.ProductoId == productId)
                    .Select(d => d.NotaPedidoId)
                    .Distinct()
                    .Take(200)
                    .ToListAsync();

                if (orderIds.Any())
                {
                    var rawStats = await _context.NotasPedidoDets
                        .AsNoTracking()
                        .Where(d => orderIds.Contains(d.NotaPedidoId) && !currentIds.Contains(d.ProductoId)) 
                        .GroupBy(d => d.ProductoId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(limit + 5)
                        .ToListAsync();

                    // Mapeamos a DTO
                    if (rawStats.Any())
                    {
                        var statProducts = await GetProductsByIdsAsync(rawStats);
                        // Añadimos razón genérica
                        foreach(var p in statProducts) { p.Razon = "Frecuentemente comprado junto"; }
                        
                        // Filtramos duplicados en memoria por si acaso
                        var newStats = statProducts.Where(p => !finalRecommendations.Any(fr => fr.Id == p.Id))
                                                   .Take(limit - finalRecommendations.Count);
                        
                        finalRecommendations.AddRange(newStats);
                    }
                }
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating stats: {ex.Message}");
            }
            
            return finalRecommendations.Take(limit);
        }

        private async Task<List<ProductDto>> GetStrategiesRecommendations(Producto product)
        {
            var recs = new List<ProductDto>();
            string name = product.Nombre;

            // 🖱️ Caso 1: Mouse
            if (ContainsAny(name, "MOUSE", "RATON"))
            {
                recs.AddRange(await FindComplements(new[] { "PAD", "ALFOMBRILLA" }, "Para que se deslice bien y no raye la mesa"));
                recs.AddRange(await FindComplements(new[] { "PILA", "BATERIA" }, "Si es inalámbrico, sin energía no sirve"));
                recs.AddRange(await FindComplements(new[] { "TECLADO" }, "Si el mouse está viejo, el teclado suele estarlo también"));
            }
            // 🖨️ Caso 2: Tinta
            else if (ContainsAny(name, "TINTA", "CARTUCHO", "TONER"))
            {
                recs.AddRange(await FindComplements(new[] { "PAPEL BOND", "RESMA" }, "Sin papel no hay impresión"));
                recs.AddRange(await FindComplements(new[] { "LIMPIEZA", "KIT LIMPIEZA" }, "Evita que se tape la impresora"));
                recs.AddRange(await FindComplements(new[] { "FOTOGRAFIC" }, "Si es tinta de calidad, puede imprimir fotos"));
            }
            // 🖥️ Caso 3: Monitor
            else if (ContainsAny(name, "MONITOR", "PANTALLA"))
            {
                recs.AddRange(await FindComplements(new[] { "STAND", "SOPORTE" }, "Evita dolor de cuello (ergonomía)"));
                recs.AddRange(await FindComplements(new[] { "CAMARA", "WEB", "WEBCAM" }, "Para videollamadas"));
                recs.AddRange(await FindComplements(new[] { "PARLANTE", "HEADSET", "AUDIFONO" }, "Muchos monitores no traen sonido"));
                recs.AddRange(await FindComplements(new[] { "ESTABILIZADOR" }, "Protege de subidas de luz"));
            }
            // ⚡ Caso 4: RAM o SSD (Actualización)
            else if (ContainsAny(name, "RAM", "DDR", "SSD", "SOLID", "DISCO SOLIDO"))
            {
                 // Nota: "Servicio Técnico" debería ser un producto en la BD. Si no existe, buscamos equivalentes.
                 // Asumiendo que existen productos de servicio o herramientas.
                recs.AddRange(await FindComplements(new[] { "SERVICIO", "INSTALACION", "SOPORTE TECNICO" }, "El cliente no sabe instalar, véndele el servicio"));
                recs.AddRange(await FindComplements(new[] { "MANTENIMIENTO", "LIMPIEZA PC", "AIRE COMPRIMIDO" }, "Ya que se abre la PC, se aprovecha para limpiar"));
                recs.AddRange(await FindComplements(new[] { "SOFTWARE", "OFFICE", "WINDOWS" }, "Para que el SSD se sienta nuevo"));
            }
            // 💼 Caso 5: Estuche para Laptop / Laptop
            else if (ContainsAny(name, "ESTUCHE", "FUNDA", "MALETIN", "MOCHILA", "LAPTOP", "NOTEBOOK"))
            {
                recs.AddRange(await FindComplements(new[] { "MOUSE INALAMBRICO", "MOUSE BLUETOOTH" }, "El touchpad cansa"));
                recs.AddRange(await FindComplements(new[] { "MOCHILA" }, "Para llevar cargador y cuadernos"));
                recs.AddRange(await FindComplements(new[] { "COOLER", "BASE" }, "Evita sobrecalentamiento"));
            }
            // 📂 Caso 6: Disco Duro Externo
            else if (ContainsAny(name, "EXTERNO", "DISCO DURO", "HDD"))
            {
                recs.AddRange(await FindComplements(new[] { "ESTUCHE", "FUNDA" }, "Para llevarlo seguro"));
                recs.AddRange(await FindComplements(new[] { "CABLE USB", "ADAPTADOR" }, "Para transferir rápido"));
                recs.AddRange(await FindComplements(new[] { "ANTIVIRUS" }, "Protege los archivos"));
            }
            // 🖨️ Caso 7: Impresora
            else if (ContainsAny(name, "IMPRESORA", "MULTIFUNCIONAL"))
            {
                recs.AddRange(await FindComplements(new[] { "TINTA", "BOTELLA" }, "Es el combustible"));
                recs.AddRange(await FindComplements(new[] { "PAPEL", "ETIQUETA" }, "Para imprimir"));
            }

            return recs;
        }

        private async Task<List<ProductDto>> FindComplements(string[] searchTerms, string reason)
        {
            var foundProducts = new List<ProductDto>();
            
            // Intentamos buscar productos que coincidan con ALGUNO de los términos
            // Hacemos una búsqueda simple iterativa para asegurar match
            
            foreach (var term in searchTerms)
            {
                var match = await _context.Productos
                    .AsNoTracking()
                    .Where(p => !p.Inactivo && (p.Servicio == false || term.Contains("SERVICIO") || term.Contains("INSTALACION")) &&  // Permitir servicios solo si buscamos servicios
                                EF.Functions.ILike(p.Nombre, $"%{term}%"))
                    .OrderByDescending(p => p.EcomPrecio)
                    .FirstOrDefaultAsync();

                if (match != null)
                {
                    foundProducts.Add(new ProductDto
                    {
                        Id = match.Id,
                        Codigo = match.Codigo,
                        Nombre = match.Nombre,
                        Descripcion = match.Descripcion,
                        EcomPrecio = match.EcomPrecio,
                        Razon = reason
                    });
                    
                    // Solo necesitamos 1 recomendación por "Categoría de Razón" para no saturar
                    break; 
                }
            }
            return foundProducts;
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
                .Where(p => productIds.Contains(p.Id) && !p.Servicio) 
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

        private bool ContainsAny(string text, params string[] terms)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var term in terms)
            {
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
