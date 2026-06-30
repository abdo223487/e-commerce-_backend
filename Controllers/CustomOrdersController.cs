using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/custom-orders")]
    [Authorize]
    [Produces("application/json")]
    public class CustomOrdersController : ControllerBase
    {
        private readonly ICustomOrderService _customOrderService;

        public CustomOrdersController(ICustomOrderService customOrderService)
        {
            _customOrderService = customOrderService;
        }

        /// <summary>User: start a new custom order request.</summary>
        [HttpPost]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(CustomOrderResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateCustomOrderDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _customOrderService.CreateAsync(User.GetUserId(), User.GetUserName(), dto);
            return ok ? CreatedAtAction(nameof(GetById), new { id = data!.Id }, data) : BadRequest(new { error });
        }

        /// <summary>User: own custom orders.</summary>
        [HttpGet]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(List<CustomOrderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _customOrderService.GetForUserAsync(User.GetUserId()));
        }

        /// <summary>Admin: all custom orders (includes userId/userName).</summary>
        [HttpGet("admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(List<AdminCustomOrderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllAdmin()
        {
            return Ok(await _customOrderService.GetAllAsync());
        }

        /// <summary>User: view own custom order with messages.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(CustomOrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _customOrderService.GetByIdAsync(id, User.GetUserId(), isAdmin: false);
            return order is null ? NotFound(new { error = "Custom order not found." }) : Ok(order);
        }

        /// <summary>Admin: view any custom order with messages (includes userId/userName).</summary>
        [HttpGet("{id:guid}/admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(AdminCustomOrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByIdAdmin(Guid id)
        {
            var order = await _customOrderService.GetByIdAsync(id, restrictToUserId: null, isAdmin: true);
            return order is null ? NotFound(new { error = "Custom order not found." }) : Ok(order);
        }

        /// <summary>User or admin: post a message in the thread.</summary>
        [HttpPost("{id:guid}/messages")]
        [ProducesResponseType(typeof(CustomOrderMessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddMessage(Guid id, [FromBody] CreateCustomOrderMessageDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var isAdmin = User.IsAdmin();
            var restrictTo = isAdmin ? (Guid?)null : User.GetUserId();

            var (ok, error, data) = await _customOrderService.AddMessageAsync(
                id, User.GetUserId(), User.GetUserName(), isAdmin, dto, restrictTo);

            return ok ? Ok(data) : BadRequest(new { error });
        }

        /// <summary>Admin: update conversation status (Open / InProgress / Closed).</summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(AdminCustomOrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateCustomOrderStatusDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _customOrderService.UpdateStatusAsync(id, dto.Status);
            return ok ? Ok(data) : NotFound(new { error });
        }

        /// <summary>Admin: delete a custom order thread entirely.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (ok, error) = await _customOrderService.DeleteAsync(id);
            return ok ? NoContent() : NotFound(new { error });
        }
    }
}
