using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize]
    [Produces("application/json")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>User: place a new order.</summary>
        [HttpPost]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _orderService.CreateOrderAsync(User.GetUserId(), dto);
            return ok ? CreatedAtAction(nameof(GetById), new { id = data!.Id }, data) : BadRequest(new { error });
        }

        /// <summary>User: their own orders.</summary>
        [HttpGet]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(List<OrderResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _orderService.GetOrdersForUserAsync(User.GetUserId()));
        }

        /// <summary>Admin/Supervisor: all orders.</summary>
        [HttpGet("admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(List<AdminOrderResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllAdmin()
        {
            return Ok(await _orderService.GetAllOrdersAsync());
        }

        /// <summary>User: their own order by id.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _orderService.GetOrderByIdAsync(id, User.GetUserId());
            return order is null ? NotFound(new { error = "Order not found." }) : Ok(order);
        }

        /// <summary>Admin/Supervisor: any order by id.</summary>
        [HttpGet("{id:guid}/admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(AdminOrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAdmin(Guid id)
        {
            var adminOrder = await _orderService.GetOrderByIdAdminAsync(id);
            return adminOrder is null ? NotFound(new { error = "Order not found." }) : Ok(adminOrder);
        }

        /// <summary>User: tracking timeline for their own order.</summary>
        [HttpGet("{id:guid}/timeline")]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(List<OrderTimelineEntryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTimeline(Guid id)
        {
            var order = await _orderService.GetOrderByIdAsync(id, User.GetUserId());
            return order is null ? NotFound(new { error = "Order not found." }) : Ok(order.Timeline);
        }

        /// <summary>Admin/Supervisor: full tracking timeline (with actor identity) for any order.</summary>
        [HttpGet("{id:guid}/timeline/admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(List<AdminOrderTimelineEntryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTimelineAdmin(Guid id)
        {
            var order = await _orderService.GetOrderByIdAdminAsync(id);
            return order is null ? NotFound(new { error = "Order not found." }) : Ok(order.Timeline);
        }

        /// <summary>Admin/Supervisor: change order status.</summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(AdminOrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _orderService.UpdateStatusAsync(id, dto.Status, User.GetUserId(), dto.Note);
            return ok ? Ok(data) : BadRequest(new { error });
        }

        /// <summary>User: delete own pending order. Admin/Supervisor: delete any order.</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var isAdminOrSup = User.IsAdmin();
            var restrictTo = isAdminOrSup ? (Guid?)null : User.GetUserId();

            var (ok, error) = await _orderService.DeleteOrderAsync(id, restrictTo, isAdminOrSup, User.GetUserId());
            return ok ? NoContent() : BadRequest(new { error });
        }
    }
}
