using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using ProductRecommender.Backend.Models.Core;

namespace ProductRecommender.Backend.Services
{
    public class ProductEntry
    {
        [KeyType(count: 200000)] // Adjust count based on max ProductId
        public uint ProductId { get; set; }
        
        [KeyType(count: 200000)] // Using Co-Product as 'User' for Item-Item similarity
        public uint CoProductId { get; set; }
        
        public float Label { get; set; }
    }

    public class ProductPrediction
    {
        public float Score { get; set; }
    }

    public class MLService
    {
        private readonly UpgradedbContext _context;
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private static string ModelPath = "product_model.zip";

        public MLService(UpgradedbContext context)
        {
            _context = context;
            _mlContext = new MLContext();
        }

        public async Task TrainModel()
        {
            // 1. Load Data: Create pairs of products bought together
            // We use a simplified approach: specific product pairs from recent orders
            
            var rawData = await _context.NotasPedidoDets
                .AsNoTracking()
                .OrderByDescending(d => d.Id)
                .Take(5000) // Limit training data for speed
                .Select(d => new { d.NotaPedidoId, d.ProductoId })
                .ToListAsync();

            var grouped = rawData.GroupBy(d => d.NotaPedidoId).ToList();
            var pairs = new List<ProductEntry>();

            foreach (var order in grouped)
            {
                var products = order.Select(d => d.ProductoId).ToList();
                for (int i = 0; i < products.Count; i++)
                {
                    for (int j = 0; j < products.Count; j++)
                    {
                        if (i == j) continue;
                        
                        pairs.Add(new ProductEntry
                        {
                            ProductId = (uint)products[i],
                            CoProductId = (uint)products[j],
                            Label = 1 // Bought together
                        });
                    }
                }
            }

            if (!pairs.Any()) return;

            // 2. Train Matrix Factorization Model
            var trainingData = _mlContext.Data.LoadFromEnumerable(pairs);

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = nameof(ProductEntry.ProductId),
                MatrixRowIndexColumnName = nameof(ProductEntry.CoProductId),
                LabelColumnName = "Label",
                LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
                Alpha = 0.01,
                Lambda = 0.025
            };

            var trainer = _mlContext.Recommendation().Trainers.MatrixFactorization(options);
            _model = trainer.Fit(trainingData);

            // Save model if needed
            // _mlContext.Model.Save(_model, trainingData.Schema, ModelPath);
        }

        public async Task<List<int>> GetRecommendedProductIds(int productId, int limit = 5)
        {
            if (_model == null)
            {
                await TrainModel(); // Lazy training
            }

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductEntry, ProductPrediction>(_model);

            // Predict scores for other products against the target productId
            // In a real scenario, you iterate over a candidate set (e.g. top 100 popular items) to score them
            
            var candidateIds = await _context.Productos
                .AsNoTracking()
                .Where(p => !p.Inactivo && p.Id != productId)
                .Select(p => p.Id)
                .Take(200) // Score against top 200 candidates to save time
                .ToListAsync();

            var scoredList = new List<(int Id, float Score)>();

            foreach (var candidateId in candidateIds)
            {
                var prediction = predictionEngine.Predict(new ProductEntry
                {
                    ProductId = (uint)productId,
                    CoProductId = (uint)candidateId
                });
                scoredList.Add((candidateId, prediction.Score));
            }

            return scoredList.OrderByDescending(x => x.Score)
                             .Take(limit)
                             .Select(x => x.Id)
                             .ToList();
        }
    }
}
