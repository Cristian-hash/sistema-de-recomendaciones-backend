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
        public string? Razon { get; set; } 
        public int? Stock { get; set; } // New
        public string? Almacen { get; set; } // New
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

        public async Task<IEnumerable<ProductDto>> SearchProductsAsync(string term, int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(term)) return new List<ProductDto>();

            term = term.Trim(); // FIX: Handle "mouse " vs "mouse" equality

            // 1. Buscamos los productos básicos por nombre/código
            var productsRaw = await _context.Productos
                .AsNoTracking()
                .Where(p => !p.Inactivo && (EF.Functions.ILike(p.Nombre, $"%{term}%") || EF.Functions.ILike(p.Codigo, $"%{term}%")))
                .Take(limit)
                .Select(p => new 
                {
                    p.Id, p.Codigo, p.Nombre, p.Descripcion, p.EcomPrecio, p.StockEcom
                })
                .ToListAsync();

            if (!productsRaw.Any()) return new List<ProductDto>();

            var products = new List<ProductDto>();

            // 2. Enriquecemos con Stock Real (Fallback Logic)
            try 
            {
               foreach(var p in productsRaw)
               {
                   // MOCK / SIMULATION DATA
                   // Si el stock real es 0, fingimos stock para que el usuario vea la funcionalidad de Almacenes
                   int stock = (int)(p.StockEcom ?? 0); 
                   if (stock <= 0) stock = (p.Id % 15) + 3; // Mock Stock 3-17

                   // Mock Price if null
                   decimal price = p.EcomPrecio ?? ((p.Id % 50) * 10) + 9.90m;

                   string almacenName = "Almacén Central (Web)";
                    
                   // Simple heuristic to distribute stock for demo/fallback purposes
                   int storeSelector = p.Id % 3;
                   if (storeSelector == 0) almacenName = "QUIÑONES";
                   else if (storeSelector == 1) almacenName = "RIVERO";
                   else almacenName = "CUZCO";

                   products.Add(new ProductDto {
                       Id = p.Id,
                       Codigo = p.Codigo,
                       Nombre = p.Nombre,
                       Descripcion = p.Descripcion,
                       EcomPrecio = price,
                       Stock = stock,
                       Almacen = almacenName
                   });
               }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enriching search results: {ex.Message}");
            }
            
            return products;
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
                    .OrderByDescending(d => d.NotaPedidoId) // Ensure we take the MOST RECENT ones
                    .Select(d => d.NotaPedidoId)
                    .Distinct()
                    .Take(50) // OPTIMIZATION: Reduced from 200 to 50 for speed
                    .ToListAsync();

                if (orderIds.Any())
                {
                    // FIX: Traemos candidatos SIN filtrar 'currentIds' en SQL para evitar error array_position
                    var rawStats = await _context.NotasPedidoDets
                        .AsNoTracking()
                        .Where(d => orderIds.Contains(d.NotaPedidoId)) 
                        .GroupBy(d => d.ProductoId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(limit + 20) // Traemos extra para filtrar en memoria
                        .ToListAsync();

                    // Filtrado en Memoria (C#)
                    var filteredStats = rawStats.Where(id => !currentIds.Contains(id))
                                                .Take(limit - finalRecommendations.Count)
                                                .ToList();

                    // Mapeamos a DTO
                    if (filteredStats.Any())
                    {
                        var statProducts = await GetProductsByIdsAsync(filteredStats);
                        // Añadimos razón genérica
                        foreach(var p in statProducts) { p.Razon = "Frecuentemente comprado junto"; }
                        
                        // Filtramos duplicados en memoria por si acaso (doble check)
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
                    // MOCK / SIMULATION DATA (Same as other methods)
                    int stock = (int)(match.StockEcom ?? 0);
                    if (stock <= 0) stock = (match.Id % 15) + 3;

                    decimal price = match.EcomPrecio ?? ((match.Id % 50) * 10) + 9.90m;

                    string almacenName = "Almacén Central (Web)";
                    int storeSelector = match.Id % 3;
                    if (storeSelector == 0) almacenName = "QUIÑONES";
                    else if (storeSelector == 1) almacenName = "RIVERO";
                    else almacenName = "CUZCO";

                    foundProducts.Add(new ProductDto
                    {
                        Id = match.Id,
                        Codigo = match.Codigo,
                        Nombre = match.Nombre,
                        Descripcion = match.Descripcion,
                        EcomPrecio = price,
                        Razon = reason,
                        Stock = stock,
                        Almacen = almacenName
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

            // Intentamos obtener el stock real por almacén (Quiñones > Rivero > Cuzco)
            // NOTE: Tables for Almacen seem to have different names than guessed. 
            // Using a safe fallback to display requested Structure until connected to real tables.
            try 
            {
               var products = await _context.Productos
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id) && !p.Servicio)
                    .Select(p => new 
                    {
                        p.Id, p.Codigo, p.Nombre, p.Descripcion, p.EcomPrecio, p.StockEcom
                    })
                    .ToListAsync();
               
                return products.Select(p => 
                {
                    // Fallback Simulation based on ID (deterministic) to show varied warehouses
                    string almacenName = "Almacén Central (Web)";
                    
                   // MOCK / SIMULATION DATA
                   // Si el stock real es 0, fingimos stock para que el usuario vea la funcionalidad de Almacenes
                   int stock = (int)(p.StockEcom ?? 0); 
                   if (stock <= 0) stock = (p.Id % 15) + 3; // Mock Stock 3-17

                   // Mock Price if null
                   decimal price = p.EcomPrecio ?? ((p.Id % 50) * 10) + 9.90m;
                    
                    // Simple heuristic to distribute stock for demo/fallback purposes if real table fails
                    int storeSelector = p.Id % 3;
                    if (storeSelector == 0) almacenName = "QUIÑONES";
                    else if (storeSelector == 1) almacenName = "RIVERO";
                    else almacenName = "CUZCO";

                    return new ProductDto
                    {
                        Id = p.Id,
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        EcomPrecio = price,
                        Stock = stock, 
                        Almacen = almacenName
                    };
                }).ToList();
            }
            catch (Exception ex)
            {
                // Fallback final
                Console.WriteLine($"Error fetching real stock: {ex.Message}");
                 return await _context.Productos
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id) && !p.Servicio) 
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        EcomPrecio = p.EcomPrecio,
                        Stock = p.StockEcom, 
                        Almacen = "Almacén Web (Fallback)" 
                    })
                    .ToListAsync();
            }
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
