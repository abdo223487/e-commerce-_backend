using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    [Produces("application/json")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>Public: list all reviews (no userId).</summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ReviewResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _reviewService.GetAllAsync(isAdmin: false));
        }

        /// <summary>Admin: list all reviews (includes userId in each item, plus the reviewer's name).</summary>
        [HttpGet("admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(List<AdminReviewResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllAdmin()
        {
            return Ok(await _reviewService.GetAllAsync(isAdmin: true));
        }

        /// <summary>Public: get a review. Admin response includes userId.</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var isAdmin = User.Identity?.IsAuthenticated == true && User.IsAdmin();
            var review = await _reviewService.GetByIdAsync(id, isAdmin);
            return review is null ? NotFound(new { error = "Review not found." }) : Ok(review);
        }

        /// <summary>User: leave a review. Earns coins automatically.</summary>
        [HttpPost]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _reviewService.CreateAsync(User.GetUserId(), dto);
            return ok ? CreatedAtAction(nameof(GetById), new { id = data!.Id }, data) : BadRequest(new { error });
        }

        /// <summary>User: edit own review. Admin: edit any review.</summary>
        [HttpPut("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var restrictTo = User.IsAdmin() ? (Guid?)null : User.GetUserId();
            var (ok, error, data) = await _reviewService.UpdateAsync(id, dto, restrictTo);
            return ok ? Ok(data) : NotFound(new { error });
        }

        /// <summary>User: delete own review. Admin: delete any review.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var restrictTo = User.IsAdmin() ? (Guid?)null : User.GetUserId();
            var (ok, error) = await _reviewService.DeleteAsync(id, restrictTo);
            return ok ? NoContent() : NotFound(new { error });
        }
    }
}
