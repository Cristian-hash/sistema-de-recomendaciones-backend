using Microsoft.AspNetCore.Mvc;
using ProductRecommender.Backend.Models.Core;
using ProductRecommender.Backend.Services;

namespace ProductRecommender.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecommendationsController : ControllerBase
    {
        private readonly IRecommendationService _recommendationService;

        public RecommendationsController(IRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> Search([FromQuery] string q)
        {
            var results = await _recommendationService.SearchProductsAsync(q);
            return Ok(results);
        }

        [HttpGet("{productId}")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetRecommendations(int productId)
        {
            var recommendations = await _recommendationService.GetRecommendationsAsync(productId);
            return Ok(recommendations);
        }
    }
}
