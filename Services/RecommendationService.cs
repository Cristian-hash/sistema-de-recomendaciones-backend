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
        public int? Stock { get; set; } 
        public string? Almacen { get; set; } 
        public List<string> Features { get; set; } = new List<string>(); 
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

            term = term.Trim();

            // 1. Buscamos los productos básicos por nombre/código
            var productsRaw = await _context.Productos
                .AsNoTracking()
                .Where(p => !p.Inactivo && (EF.Functions.ILike(p.Nombre, $"%{term}%") || EF.Functions.ILike(p.Codigo, $"%{term}%")))
                .Take(limit)
                .Select(p => new 
                {
                    p.Id, p.Codigo, p.Nombre, p.Descripcion, p.EcomPrecio, p.StockEcom, p.EcommerceDescrip
                })
                .ToListAsync();

            var productsDtos = productsRaw.Select(p => new ProductDto 
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                EcomPrecio = p.EcomPrecio,
                Features = ExtractFeatures(p.EcommerceDescrip ?? p.Descripcion ?? "")
            }).ToList();

            if (!productsDtos.Any()) return new List<ProductDto>();
            
            await EnrichProductsWithStockAsync(productsDtos);
            
            return productsDtos;
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

                var orderIdsNullable = await _context.NotasPedidoDets
                    .AsNoTracking()
                    .Where(d => d.ProductoId == productId)
                    .OrderByDescending(d => d.NotaPedidoId) 
                    .Select(d => d.NotaPedidoId)
                    .Distinct()
                    .Take(50) 
                    .ToListAsync();

                // FIX: Ensure no nulls and work with int to avoid "array_position(integer[], unknown)" error
                var orderIds = orderIdsNullable.Where(id => id.HasValue).Select(id => id.Value).ToList();

                if (orderIds.Any())
                {
                    var rawStats = await _context.NotasPedidoDets
                        .AsNoTracking()
                        .Where(d => d.NotaPedidoId.HasValue && orderIds.Contains(d.NotaPedidoId.Value)) 
                        .GroupBy(d => d.ProductoId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(limit + 20) 
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
                // USER REQUEST: "sácame todos los teclados". We prioritize Keyboards and show more of them.
                recs.AddRange(await FindComplements(new[] { "TECLADO" }, "Si el mouse está viejo, el teclado suele estarlo también", count: 3));
                recs.AddRange(await FindComplements(new[] { "PAD", "ALFOMBRILLA" }, "Para que se deslice bien y no raye la mesa", count: 1));
                recs.AddRange(await FindComplements(new[] { "PILA", "BATERIA" }, "Si es inalámbrico, sin energía no sirve", count: 1));
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

        private async Task<List<ProductDto>> FindComplements(string[] searchTerms, string reason, int count = 1)
        {
            var foundProducts = new List<ProductDto>();
            
            foreach (var term in searchTerms)
            {
                // Fetch CANDIDATES first (without filtering by stock yet)
                // We take e.g. 50 topmost expensive/relevant items to check their stock
                var candidatesRaw = await _context.Productos
                    .AsNoTracking()
                    .Where(p => !p.Inactivo && (p.Servicio == false || term.Contains("SERVICIO") || term.Contains("INSTALACION")) &&
                                EF.Functions.ILike(p.Nombre, $"%{term}%"))
                    .OrderByDescending(p => p.EcomPrecio) // Strategy: Recommend expensive/premium first
                    .Take(50) 
                    .Select(p => new ProductDto 
                    {
                        Id = p.Id,
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        EcomPrecio = p.EcomPrecio,
                        Razon = reason,
                        Features = ExtractFeatures(p.EcommerceDescrip ?? p.Descripcion ?? "")
                    })
                    .ToListAsync();

                if (candidatesRaw.Any())
                {
                    // Enrich batch with stock
                    await EnrichProductsWithStockAsync(candidatesRaw);
                    
                    // Filter ONLY those with stock > 0
                    var available = candidatesRaw.Where(p => p.Stock > 0).Take(count).ToList();
                    
                    foundProducts.AddRange(available);
                    
                    if (foundProducts.Count >= count) break;
                }
            }
            
            // Limit total per call just in case
            return foundProducts.Take(count).ToList();
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

            try 
            {
               var productsRaw = await _context.Productos
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id)) 
                    .Select(p => new 
                    {
                        p.Id, p.Codigo, p.Nombre, p.Descripcion, p.EcomPrecio, p.StockEcom, p.EcommerceDescrip
                    })
                    .ToListAsync();
               
                var productDtos = productsRaw.Select(p => new ProductDto
                {
                    Id = p.Id,
                    Codigo = p.Codigo,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    EcomPrecio = p.EcomPrecio,
                    Features = ExtractFeatures(p.EcommerceDescrip ?? p.Descripcion ?? "")
                }).ToList();

                await EnrichProductsWithStockAsync(productDtos);

                return productDtos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching products by ids: {ex.Message}");
                return new List<ProductDto>();
            }
        }

        private async Task EnrichProductsWithStockAsync(List<ProductDto> productsDtos)
        {
             if (!productsDtos.Any()) return;

            var productIds = productsDtos.Select(p => p.Id).ToList();

            try 
            {
                // A. Stock Real GLOBAL (Count Articulos)
                var stockRealDict = await _context.Articulos
                    .Where(a => productIds.Contains(a.ProductoId) && !a.Inactivo && a.AlmacenId != null)
                    .GroupBy(a => a.ProductoId)
                    .Select(g => new { g.Key, Cantidad = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Cantidad);
                    
                // A2. Stock Real Detallado
                var stockRealDetalle = await _context.Articulos
                    .Where(a => productIds.Contains(a.ProductoId) && !a.Inactivo && a.AlmacenId != null)
                    .GroupBy(a => new { a.ProductoId, a.AlmacenId })
                    .Select(g => new { g.Key.ProductoId, g.Key.AlmacenId, Cantidad = g.Count() })
                    .ToListAsync();

                // B. Stock Comprometido GLOBAL
                var stockComprometidoDict = await _context.NotasPedidoDets
                    .Include(d => d.NotaPedido)
                    .Where(d => productIds.Contains(d.ProductoId) && 
                                !d.EntregaCompleta && 
                                !d.NotaPedido.Anulada)
                    .GroupBy(d => d.ProductoId)
                    .Select(g => new { g.Key, Cantidad = g.Sum(x => x.Cantidad - (x.CantidadEntregada ?? 0)) })
                    .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

                // C. Nombres de Almacenes
                var almacenIds = stockRealDetalle.Select(s => s.AlmacenId).Distinct().OfType<int>().ToList();
                var almacenes = await _context.Almacenes
                    .Where(a => almacenIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, a => a.Nombre);

                foreach(var p in productsDtos)
                {
                    if (p.Features.Count == 0 && !string.IsNullOrEmpty(p.Nombre)) p.Features = GenerateMockFeatures(p.Nombre);

                    // 1. Stock Disponible Global
                    int real = stockRealDict.ContainsKey(p.Id) ? stockRealDict[p.Id] : 0;
                    int comprometido = stockComprometidoDict.ContainsKey(p.Id) ? (int)stockComprometidoDict[p.Id] : 0;
                    int disponible = real - comprometido;

                    p.Stock = disponible > 0 ? disponible : 0;

                    // 2. Determinar Nombre de Almacén
                    if (p.Stock > 0)
                    {
                        var stocks = stockRealDetalle
                                    .Where(s => s.ProductoId == p.Id && s.AlmacenId.HasValue)
                                    .OrderByDescending(s => s.Cantidad)
                                    .ToList();
                        
                        if (stocks.Count == 1)
                        {
                            if (almacenes.ContainsKey(stocks[0].AlmacenId!.Value))
                                p.Almacen = almacenes[stocks[0].AlmacenId!.Value];
                            else
                                p.Almacen = "Almacén ID " + stocks[0].AlmacenId;
                        }
                        else if (stocks.Count > 1)
                        {
                            var principal = stocks.First();
                            string nombrePrincipal = almacenes.ContainsKey(principal.AlmacenId!.Value) ? 
                                                     almacenes[principal.AlmacenId!.Value] : "Almacén";
                            
                            p.Almacen = $"{nombrePrincipal} (+{stocks.Count - 1})";
                        }
                        else 
                        {
                             p.Almacen = "Disponible";
                        }
                    }
                    else
                    {
                        p.Almacen = "Sin Stock";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enriching stock: {ex.Message}");
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

        private static List<string> ExtractFeatures(string description)
        {
            var features = new List<string>();
            if (string.IsNullOrWhiteSpace(description)) return features;

            var lines = description.Split(new[] { '\n', ';', '|', '•' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var clean = line.Trim();
                if (clean.Length > 2 && clean.Length < 60 && !clean.StartsWith("http"))
                {
                    features.Add(clean);
                }
            }

            return features.Take(5).ToList(); 
        }

        private static List<string> GenerateMockFeatures(string name)
        {
            var features = new List<string>();
            var upper = name.ToUpper();

            if (upper.Contains("PROCESADOR") || upper.Contains("CPU") || upper.Contains("CORE") || upper.Contains("RYZEN"))
            {
                 if (upper.Contains("I9")) features.Add("Rendimiento Extremo: Core i9");
                 else if (upper.Contains("I7")) features.Add("Alto Rendimiento: Core i7");
                 else if (upper.Contains("I5")) features.Add("Rendimiento Equilibrado: Core i5");
                 else if (upper.Contains("I3")) features.Add("Uso Básico: Core i3");
                 else if (upper.Contains("RYZEN 9")) features.Add("Multitarea Pesada: Ryzen 9");
                 else if (upper.Contains("RYZEN 7")) features.Add("Gaming/Streaming: Ryzen 7");
                 else if (upper.Contains("RYZEN 5")) features.Add("Gaming Calidad/Precio: Ryzen 5");
                 else if (upper.Contains("RYZEN 3")) features.Add("Ofimática: Ryzen 3");
                 else features.Add("Procesador de Escritorio");
            }
            else if (upper.Contains("MOUSE"))
            {
                features.Add(upper.Contains("GAMER") ? "Sensor Óptico Gamer" : "Diseño Ergonómico");
            }
            else if (upper.Contains("TECLADO"))
            {
                features.Add(upper.Contains("MECANICO") ? "Switches Mecánicos Durables" : "Escritura Silenciosa");
            }
            else if (upper.Contains("MONITOR") || upper.Contains("PANTALLA"))
            {
                if (upper.Contains("144HZ") || upper.Contains("165HZ")) features.Add("Alta Fluidez Gaming");
                else if (upper.Contains("4K")) features.Add("Ultra Alta Definición");
                else if (upper.Contains("IPS")) features.Add("Colores Vivos (IPS)");
                else features.Add("Pantalla Nítida");
            }
            else if (upper.Contains("SSD") || upper.Contains("DISK"))
            {
                features.Add("Carga Rápida de Sistema");
            }
            else
            {
                features.Add("Calidad Recomendada"); 
            }

            return features.Take(1).ToList(); 
        }
    }
}
