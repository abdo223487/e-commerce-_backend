using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/search")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        /// <summary>Typo-tolerant product search, e.g. GET /api/search?q=...</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int max = 20)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { error = "Query parameter 'q' is required." });

            var results = await _searchService.SearchProductsAsync(q, Math.Clamp(max, 1, 50));
            return Ok(results);
        }
    }

    [ApiController]
    [Route("api/recommendations")]
    [Authorize(Policy = "UserOnly")]
    [Produces("application/json")]
    public class RecommendationsController : ControllerBase
    {
        private readonly IRecommendationService _recommendationService;

        public RecommendationsController(IRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        /// <summary>
        /// Personalized product recommendations based on the categories the
        /// user bought from most in their last 2 orders.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get([FromQuery] int max = 12)
        {
            var results = await _recommendationService.GetRecommendationsAsync(User.GetUserId(), Math.Clamp(max, 1, 50));
            return Ok(results);
        }
    }
}
