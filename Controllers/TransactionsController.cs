using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/supervisor/transactions")]
    [Authorize(Policy = "SupervisorOnly")]
    [Produces("application/json")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionsController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>Supervisor: create a new transaction (description, price, phoneNumber, typeId).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(TransactionResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _transactionService.CreateAsync(User.GetUserId(), dto);
            return ok ? CreatedAtAction(nameof(GetLast4), null, data) : BadRequest(new { error });
        }

        /// <summary>Supervisor: get their last 4 transactions.</summary>
        [HttpGet("last4")]
        [ProducesResponseType(typeof(List<TransactionResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLast4()
        {
            return Ok(await _transactionService.GetLastFourAsync(User.GetUserId()));
        }

        /// <summary>Supervisor: get ALL transactions in the system, paginated (?page=1&amp;pageSize=10).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResultDto<TransactionResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllPaged([FromQuery] PaginationQuery query)
        {
            return Ok(await _transactionService.GetAllPagedAsync(query));
        }

        /// <summary>Supervisor: update one of their transactions.</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(TransactionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransactionDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _transactionService.UpdateAsync(User.GetUserId(), id, dto);
            if (ok) return Ok(data);
            return error == "Transaction not found." ? NotFound(new { error }) : BadRequest(new { error });
        }

        /// <summary>Supervisor: delete one of their transactions.</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (ok, error) = await _transactionService.DeleteAsync(User.GetUserId(), id);
            return ok ? NoContent() : NotFound(new { error });
        }
    }
}
