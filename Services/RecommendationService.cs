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
            
            return productsDtos.OrderByDescending(p => p.Stock).ThenByDescending(p => p.EcomPrecio).ToList();
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
                        
                        // MEJORA: Generamos argumentos de venta inteligentes en lugar de texto genérico
                        foreach(var p in statProducts) 
                        { 
                            p.Razon = GenerateSalesArgument(product.Nombre, p.Nombre); 
                        }
                        
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
            
            return finalRecommendations.OrderByDescending(r => r.Stock).Take(limit);
        }

        private async Task<List<ProductDto>> GetStrategiesRecommendations(Producto product)
        {
            var recs = new List<ProductDto>();
            string name = product.Nombre;

            // 💻 HÁBITAT 5: LAPTOP (Seguidor Principal) - MOVIDO ARRIBA para prioridad
            if (ContainsAny(name, "LAPTOP", "NOTEBOOK", "NB "))
            {
                recs.AddRange(await FindComplements(new[] { "MOUSE INALAMBRICO" }, "Mayor comodidad que el touchpad"));
                recs.AddRange(await FindComplements(new[] { "MOCHILA", "MALETIN", "FUNDA" }, "Protección para el transporte diario"));
                recs.AddRange(await FindComplements(new[] { "COOLER", "BASE" }, "Mejora la refrigeración en uso prolongado"));
                // FIX: "OFFICE" keyword was matching "CASE OFFICE". Changed to "MICROSOFT", "WINDOWS", "LICENCIA"
                recs.AddRange(await FindComplements(new[] { "LICENCIA", "MICROSOFT", "WINDOWS", "ANTIVIRUS", "KASPERSKY", "ESET" }, "Software esencial desde el primer día"));
            }
            // 🖱️ HÁBITAT 1: MOUSE (Seguidor)
            else if (ContainsAny(name, "MOUSE", "RATON"))
            {
                bool isGamer = ContainsAny(name, "GAMER", "GAMING", "RGB");
                bool isWireless = ContainsAny(name, "INALAMBRICO", "BLUETOOTH", "WIFI", "WIRELESS");
                bool isRechargeable = ContainsAny(name, "RECARGABLE", "BATERIA INTERNA");

                if (isGamer)
                {
                     recs.AddRange(await FindComplements(new[] { "TECLADO GAMER", "TECLADO MECANICO" }, "Completa tu setup gaming para mejor rendimiento", count: 3));
                     recs.AddRange(await FindComplements(new[] { "PAD GAMER", "ALFOMBRILLA GAMER" }, "Superficie de control y velocidad para tu sensor", count: 1));
                     recs.AddRange(await FindComplements(new[] { "AUDIFONO GAMER", "HEADSET" }, "Inmersión total en tus partidas", count: 1));
                }
                else
                {
                     recs.AddRange(await FindComplements(new[] { "TECLADO" }, "Renovación conjunta: El desgaste suele ser simultáneo", count: 3));
                     recs.AddRange(await FindComplements(new[] { "PAD", "ALFOMBRILLA" }, "Mejora el deslizamiento y protege el escritorio", count: 1));
                }

                if (isWireless && !isRechargeable)
                {
                    recs.AddRange(await FindComplements(new[] { "PILA", "BATERIA" }, "Energía de respaldo para no quedarte desconectado", count: 1));
                }
            }
            // 🖨️ HÁBITAT 2: TINTA / TONER (Estrella)
            else if (ContainsAny(name, "TINTA", "CARTUCHO", "TONER"))
            {
                // Estrategia Estrella: Fidelización y Consumo
                recs.AddRange(await FindComplements(new[] { "PAPEL BOND", "RESMA" }, "Insumo básico para imprimir sin interrupciones"));
                recs.AddRange(await FindComplements(new[] { "KIT LIMPIEZA", "LIMPIEZA" }, "Prolonga la vida útil del cabezal de impresión"));
                recs.AddRange(await FindComplements(new[] { "FOTOGRAFIC" }, "Para impresiones de alta calidad"));
            }
            // 🖥️ HÁBITAT 3: MONITOR (Lento/Intermedio)
            else if (ContainsAny(name, "MONITOR", "PANTALLA"))
            {
                recs.AddRange(await FindComplements(new[] { "STAND", "SOPORTE" }, "Evita dolor de cuello (Ergonomía)"));
                recs.AddRange(await FindComplements(new[] { "CAMARA", "WEB", "WEBCAM" }, "Indispensable para videollamadas claras"));
                recs.AddRange(await FindComplements(new[] { "PARLANTE", "HEADSET", "AUDIFONO" }, "Muchos monitores no traen sonido integrado"));
                recs.AddRange(await FindComplements(new[] { "ESTABILIZADOR" }, "Protege tu inversión de subidas de luz"));
            }
            // 🛒 HÁBITAT 4: RAM / SSD / DISCO INTERNO (Prioridad Técnica)
            else if (ContainsAny(name, "RAM", "DDR", "DIMM", "SODIMM"))
            {
                // RAM Ecosistema
                recs.AddRange(await FindComplements(new[] { "SERVICIO", "INSTALACION", "SOPORTE TECNICO" }, "El cliente no sabe colocarla correctamente (Evita errores)"));
                recs.AddRange(await FindComplements(new[] { "MANTENIMIENTO", "LIMPIEZA PC", "AIRE COMPRIMIDO" }, "Ya que se abre la PC, limpieza preventiva"));
                recs.AddRange(await FindComplements(new[] { "PASTA TERMICA" }, "Baja temperatura al procesador (aprovechando apertura)"));
                recs.AddRange(await FindComplements(new[] { "DIAGNOSTICO" }, "Evita errores por incompatibilidad de velocidad/tipo"));
            }
            else if (ContainsAny(name, "SSD", "SOLID", "SOLIDO", "NVME", "M.2", "DISCO DURO", "HDD"))
            {
                // SSD Ecosistema
                recs.AddRange(await FindComplements(new[] { "CLONACION", "MIGRACION" }, "No pierde su sistema ni archivos"));
                recs.AddRange(await FindComplements(new[] { "FORMATEO", "INSTALACION WINDOWS" }, "Arranque rápido y sistema limpio"));
                recs.AddRange(await FindComplements(new[] { "MANTENIMIENTO", "LIMPIEZA PC" }, "Aprovecha la apertura del equipo"));
                recs.AddRange(await FindComplements(new[] { "CABLE SATA", "ADAPTADOR" }, "Necesario para compatibilidad de conexión"));
                recs.AddRange(await FindComplements(new[] { "ENCLOSURE", "COFRE", "CADDY" }, "Convierte el disco antiguo en uno externo portátil"));
            }
            // 🧠 HÁBITAT 11: PROCESADOR (CPU)
            else if (ContainsAny(name, "PROCESADOR", "CPU", "RYZEN", "INTEL", "CORE I", "ATHLON"))
            {
                recs.AddRange(await FindComplements(new[] { "PASTA TERMICA" }, "Evita sobrecalentamiento crítico"));
                recs.AddRange(await FindComplements(new[] { "COOLER", "DISIPADOR", "LIQUIDA" }, "Disipa mejor el calor y alarga la vida útil"));
                recs.AddRange(await FindComplements(new[] { "ACTUALIZACION BIOS", "SERVICIO" }, "Necesario para asegurar compatibilidad de placa"));
                recs.AddRange(await FindComplements(new[] { "LIMPIEZA" }, "Mejor flujo de aire interno"));
            }
            // 🔌 HÁBITAT 12: FUENTE DE PODER (PSU)
            else if (ContainsAny(name, "FUENTE", "PODER", "PSU", "WATTS", "REAL"))
            {
                 recs.AddRange(await FindComplements(new[] { "ESTABILIZADOR" }, "Protege de subidas de voltaje"));
                 recs.AddRange(await FindComplements(new[] { "UPS", "NO BREAK" }, "Evita apagones bruscos que dañan la PC"));
                 recs.AddRange(await FindComplements(new[] { "SERVICIO", "INSTALACION" }, "Una fuente mal conectada puede quemar equipos"));
            }
            // 🎮 HÁBITAT 13: TARJETA DE VIDEO (GPU)
            else if (ContainsAny(name, "TARJETA VIDEO", "TARJETA GRAFICA", "GPU", "RTX", "GTX", "RADEON"))
            {
                 recs.AddRange(await FindComplements(new[] { "FUENTE", "CERTIFICADA" }, "La GPU consume mucha energía, asegura potencia"));
                 recs.AddRange(await FindComplements(new[] { "COOLER" }, "Mejora el flujo de aire del case"));
                 recs.AddRange(await FindComplements(new[] { "PASTA TERMICA" }, "Baja temperaturas generales del sistema"));
                 recs.AddRange(await FindComplements(new[] { "SERVICIO" }, "Instalación y drivers optimizados para rendimiento"));
            }

            // 💼 HÁBITAT 5: ESTUCHE DE LAPTOP
            else if (ContainsAny(name, "ESTUCHE", "FUNDA", "MALETIN", "MOCHILA"))
            {
                recs.AddRange(await FindComplements(new[] { "MOUSE INALAMBRICO" }, "Mayor comodidad que el touchpad"));
                recs.AddRange(await FindComplements(new[] { "COOLER", "BASE" }, "Evita sobrecalentamiento"));
            }
            // 📂 HÁBITAT 6: DISCO DURO EXTERNO (Removed internal drives logic from here)
            else if (ContainsAny(name, "EXTERNO"))
            {
                recs.AddRange(await FindComplements(new[] { "ESTUCHE", "FUNDA" }, "Protección contra golpes (Datos seguros)"));
                recs.AddRange(await FindComplements(new[] { "CABLE", "ADAPTADOR" }, "Conectividad asegurada"));
                recs.AddRange(await FindComplements(new[] { "ANTIVIRUS" }, "Evita infectar tus archivos de respaldo"));
            }
            // 🖨️ HÁBITAT 7: IMPRESORA
            else if (ContainsAny(name, "IMPRESORA", "MULTIFUNCIONAL"))
            {
                recs.AddRange(await FindComplements(new[] { "TINTA", "BOTELLA", "TONER" }, "Asegura la continuidad de impresión"));
                recs.AddRange(await FindComplements(new[] { "PAPEL", "RESMA" }, "Papel necesario para empezar a trabajar"));
                recs.AddRange(await FindComplements(new[] { "SUPRESOR", "ESTABILIZADOR" }, "Protección eléctrica para el equipo"));
            }
            // 🔌 HÁBITAT 8: CONECTIVIDAD Y CABLES (Hubs, Adaptadores)
            else if (ContainsAny(name, "HUB", "ADAPTADOR", "CONVERTIDOR", "EXTENSION USB"))
            {
                recs.AddRange(await FindComplements(new[] { "CABLE", "HDMI", "USB" }, "Asegura la longitud necesaria para tu conexión"));
                recs.AddRange(await FindComplements(new[] { "CINTILLO", "VELCRO", "ORGANIZADOR" }, "Mantén tus cables ordenados"));
            }
            // 🔋 HÁBITAT 9: ENERGÍA (Cargadores, Cables de Poder, UPS)
            else if (ContainsAny(name, "CARGADOR", "FUENTE", "CABLE PODER", "BATERIA PORTATIL"))
            {
                recs.AddRange(await FindComplements(new[] { "SUPRESOR", "PICO" }, "Protección esencial contra fluctuaciones eléctricas"));
                recs.AddRange(await FindComplements(new[] { "ADAPTADOR ENCHUFE" }, "Compatibilidad con tomas de corriente"));
            }
            // 🌐 HÁBITAT 10: REDES (Cable UTP, Patch Cord)
            else if (ContainsAny(name, "CABLE RED", "UTP", "PATCH CORD", "CAT5", "CAT6"))
            {
                recs.AddRange(await FindComplements(new[] { "CONECTOR", "RJ45" }, "Insumos necesarios para el cableado"));
                recs.AddRange(await FindComplements(new[] { "SWITCH", "ROUTER" }, "Expande tu red si necesitas más puntos"));
            }
            // 🎧 HÁBITAT 11: AUDIO / HEADSET
            else if (ContainsAny(name, "HEADSET", "AUDIFONO", "AURICULAR", "MICROFONO"))
            {
                recs.AddRange(await FindComplements(new[] { "SOPORTE", "STAND" }, "Cuida tus audífonos y mantén el orden del escritorio"));
                recs.AddRange(await FindComplements(new[] { "ADAPTADOR AUDIO", "SPLITTER", "CABLE" }, "Mayor compatibilidad con PC y consolas"));
                recs.AddRange(await FindComplements(new[] { "CAMARA", "WEBCAM" }, "Completa tu setup de comunicación/streaming"));
            }

            return recs;
        }

        private string GenerateSalesArgument(string mainProduct, string recommendedProduct)
        {
            var main = mainProduct.ToUpper();
            var rec = recommendedProduct.ToUpper();

            // 🖱️ MOUSE
            if (main.Contains("MOUSE") || main.Contains("RATON"))
            {
                if (rec.Contains("TECLADO")) return "El desgaste suele ser simultáneo, renuévalos juntos";
                if (rec.Contains("PAD") || rec.Contains("ALFOMBRILLA")) return "Mejora la precisión y protege el escritorio";
                if (rec.Contains("PILA") || rec.Contains("BATERIA")) return "Energía de respaldo indispensable";
            }

            // ⌨️ TECLADO
            if (main.Contains("TECLADO"))
            {
                if (rec.Contains("MOUSE")) return "El compañero ideal para completar el escritorio";
                if (rec.Contains("PAD") || rec.Contains("ALFOMBRILLA")) return "Mayor confort para tus muñecas y mouse";
            }
            
            // 💻 PORTÁTILES
            if (main.Contains("LAPTOP") || main.Contains("NOTEBOOK") || main.Contains("NB") || main.Contains("NB "))
            {
                if (rec.Contains("MOCHILA") || rec.Contains("FUNDA") || rec.Contains("MALETIN")) return "Protege tu inversión de golpes y caídas";
                if (rec.Contains("MOUSE")) return "Incrementa tu productividad evitando el touchpad";
                if (rec.Contains("COOLER") || rec.Contains("BASE")) return "Evita sobrecalentamiento en sesiones largas";
                if (rec.Contains("ANTIVIRUS") || rec.Contains("LICENCIA")) return "Seguridad y software esencial desde el primer día";
                if (rec.Contains("AUDIFONO") || rec.Contains("HEADSET")) return "Para videollamadas con privacidad";
            }

            // 💾 COMPONENTES INTERNOS (RAM/SSD)
            if (main.Contains("RAM") || main.Contains("DDR") || main.Contains("SSD") || main.Contains("SOLID") || main.Contains("NVME"))
            {
                if (rec.Contains("SERVICIO") || rec.Contains("INSTALACION")) return "El cliente no sabe colocarla correctamente (Evita errores)";
                if (rec.Contains("CLONACION") || rec.Contains("MIGRACION")) return "No pierde su sistema ni archivos";
                if (rec.Contains("MANTENIMIENTO") || rec.Contains("LIMPIEZA") || rec.Contains("AIRE")) return "Ya que se abre la PC, se aprovecha para limpiar";
                if (rec.Contains("PASTA")) return "Baja temperatura al procesador (aprovechando apertura)";
                if (rec.Contains("DIAGNOSTICO")) return "Evita errores por incompatibilidad de velocidad/tipo";
                if (rec.Contains("FORMATEO") || rec.Contains("WINDOWS")) return "Arranque rápido y sistema limpio";
            }

             // 🧠 PROCESADOR
            if (main.Contains("PROCESADOR") || main.Contains("CPU"))
            {
                if (rec.Contains("PASTA")) return "Evita sobrecalentamiento crítico";
                if (rec.Contains("COOLER") || rec.Contains("DISIPADOR")) return "Disipa mejor el calor y alarga la vida útil";
                if (rec.Contains("BIOS")) return "Para compatibilidad";
            }

            // 🔌 FUENTE DE PODER
            if ((main.Contains("FUENTE") && main.Contains("PODER")) || main.Contains("PSU"))
            {
                if (rec.Contains("ESTABILIZADOR")) return "Protege de subidas de voltaje";
                if (rec.Contains("UPS")) return "Evita apagones bruscos";
                if (rec.Contains("SERVICIO")) return "Mal conectada quema equipos";
            }

            // 🎮 TARJETA DE VIDEO
            if (main.Contains("TARJETA") || main.Contains("GPU"))
            {
                if (rec.Contains("FUENTE")) return "La GPU consume mucha energía (Requerido)";
                if (rec.Contains("COOLER")) return "Evita sobrecalentamiento";
            }

            // 🖥️ MONITOR
            if (main.Contains("MONITOR") || main.Contains("PANTALLA"))
            {
                if (rec.Contains("SOPORTE") || rec.Contains("STAND") || rec.Contains("BRAZO")) return "Vital para la ergonomía y evitar dolor de cuello";
                if (rec.Contains("CAMARA") || rec.Contains("WEB") || rec.Contains("WEBCAM")) return "Indispensable para videollamadas de calidad";
                if (rec.Contains("ESTABILIZADOR") || rec.Contains("SUPRESOR")) return "Protege el panel contra picos de voltaje";
                if (rec.Contains("LIMPIEZA")) return "Mantén la pantalla libre de huellas y polvo";
            }

            // 🖨️ IMPRESORA
            if (main.Contains("IMPRESORA") || main.Contains("MULTIFUNCIONAL"))
            {
                if (rec.Contains("TINTA") || rec.Contains("CARTUCHO") || rec.Contains("TONER")) return "Asegura la continuidad de impresión con repuestos";
                if (rec.Contains("PAPEL") || rec.Contains("RESMA")) return "El insumo básico para empezar a trabajar";
                if (rec.Contains("CABLE") && rec.Contains("USB")) return "Verifica si la caja incluye el cable de conexión";
            }

            // 🎧 AUDIO / HEADSET
            if (main.Contains("HEADSET") || main.Contains("AUDIFONO") || main.Contains("AURICULAR"))
            {
                if (rec.Contains("SOPORTE") || rec.Contains("STAND")) return "Evita caídas y daños en tus audífonos";
                if (rec.Contains("ADAPTADOR") || rec.Contains("SPLITTER")) return "Asegura la compatibilidad con todos tus dispositivos";
                if (rec.Contains("WEBCAM") || rec.Contains("CAMARA")) return "Ideal para reuniones o streaming de calidad";
            }

            // 🔌 CONECTIVIDAD / ENERGÍA
            if (main.Contains("CARGADOR") || main.Contains("FUENTE") || main.Contains("CABLE"))
            {
               if (rec.Contains("SUPRESOR") || rec.Contains("ESTABILIZADOR")) return "Protege tus equipos conectados de daños eléctricos";
               if (rec.Contains("ORGANIZADOR") || rec.Contains("VELCRO")) return "Orden y seguridad para tu cableado";
            }

            // ⚡ GENÉRICOS DE MANTENIMIENTO / COMPONENTES
            if (rec.Contains("PILA") || rec.Contains("BATERIA")) return "Energía de respaldo para no quedarse a medias";
            
            // FIX: Be strict about USB Storage to avoid flagging USB Mice as "backup"
            if ((rec.Contains("USB") && (rec.Contains("MEMORIA") || rec.Contains("DRIVE") || rec.Contains("FLASH") || rec.Contains("KINGSTON") || rec.Contains("SANDISK"))) || rec.Contains("PENDRIVE")) 
                return "Siempre útil para respaldar información crítica";
                
            if (rec.Contains("SUPRESOR") || rec.Contains("ESTABILIZADOR")) return "Seguro de vida eléctrico para tus equipos";
            if (rec.Contains("LIMPIEZA") || rec.Contains("ALCOHOL") || rec.Contains("AIRE")) return "Mantenimiento preventivo para que luzca como nuevo";
            if (rec.Contains("PASTA TERMICA")) return "Mejora la disipación de calor del procesador";

            // Default más amigable
            return "Comúnmente llevado junto a este producto";
        }

        private async Task<List<ProductDto>> FindComplements(string[] searchTerms, string reason, int count = 1)
        {
            var foundProducts = new List<ProductDto>();
            
            foreach (var term in searchTerms)
            {
                // Fetch CANDIDATES first 
                var candidatesRaw = await _context.Productos
                    .AsNoTracking()
                    .Where(p => !p.Inactivo && (p.Servicio == false || term.Contains("SERVICIO") || term.Contains("INSTALACION")) &&
                                EF.Functions.ILike(p.Nombre, $"%{term}%"))
                    .OrderByDescending(p => p.EcomPrecio) // Strategy: Recommend expensive/premium first
                    .Take(200) // Increased to catch cheaper items like Pads/Cables that might be pushed down
                    .Select(p => new ProductDto 
                    {
                        Id = p.Id,
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        EcomPrecio = p.EcomPrecio,
                        Razon = reason, // Default strategy reason
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
            
            // Limit total per call
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
