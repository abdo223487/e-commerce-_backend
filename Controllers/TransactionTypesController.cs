using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    /// <summary>
    /// Supervisor: "super-secret types" — named buckets that transactions belong to.
    /// Create a type by name, then list/update/delete the transactions filed under it.
    /// </summary>
    [ApiController]
    [Route("api/supervisor/transaction-types")]
    [Authorize(Policy = "SupervisorOnly")]
    [Produces("application/json")]
    public class TransactionTypesController : ControllerBase
    {
        private readonly ITransactionTypeService _typeService;

        public TransactionTypesController(ITransactionTypeService typeService)
        {
            _typeService = typeService;
        }

        /// <summary>Supervisor: create a new transaction type (name only).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(TransactionTypeResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateTransactionTypeDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _typeService.CreateAsync(dto);
            return ok ? CreatedAtAction(nameof(GetById), new { typeId = data!.Id }, data) : BadRequest(new { error });
        }

        /// <summary>Supervisor: list all transaction types.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<TransactionTypeResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll() => Ok(await _typeService.GetAllAsync());

        /// <summary>Supervisor: get a single transaction type by id.</summary>
        [HttpGet("{typeId:guid}")]
        [ProducesResponseType(typeof(TransactionTypeResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid typeId)
        {
            var type = await _typeService.GetByIdAsync(typeId);
            return type is null ? NotFound(new { error = "Transaction type not found." }) : Ok(type);
        }

        /// <summary>Supervisor: rename a transaction type.</summary>
        [HttpPut("{typeId:guid}")]
        [ProducesResponseType(typeof(TransactionTypeResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid typeId, [FromBody] UpdateTransactionTypeDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _typeService.UpdateAsync(typeId, dto);
            if (ok) return Ok(data);
            return error == "Transaction type not found." ? NotFound(new { error }) : BadRequest(new { error });
        }

        /// <summary>Supervisor: delete a transaction type (only if it has no transactions left).</summary>
        [HttpDelete("{typeId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid typeId)
        {
            var (ok, error) = await _typeService.DeleteAsync(typeId);
            if (ok) return NoContent();
            return error == "Transaction type not found." ? NotFound(new { error }) : BadRequest(new { error });
        }

        /// <summary>Supervisor: get all transactions filed under this type, paginated (?page=1&amp;pageSize=10).</summary>
        [HttpGet("{typeId:guid}/transactions")]
        [ProducesResponseType(typeof(PagedResultDto<TransactionResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransactions(Guid typeId, [FromQuery] PaginationQuery query)
        {
            var (ok, error, data) = await _typeService.GetTransactionsAsync(typeId, query);
            return ok ? Ok(data) : NotFound(new { error });
        }

        /// <summary>Supervisor: update a transaction that belongs to this type.</summary>
        [HttpPut("{typeId:guid}/transactions/{transactionId:guid}")]
        [ProducesResponseType(typeof(TransactionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTransaction(Guid typeId, Guid transactionId, [FromBody] UpdateTransactionDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _typeService.UpdateTransactionAsync(typeId, transactionId, dto);
            if (ok) return Ok(data);
            return error == "Transaction not found under this type." ? NotFound(new { error }) : BadRequest(new { error });
        }

        /// <summary>Supervisor: delete a transaction that belongs to this type.</summary>
        [HttpDelete("{typeId:guid}/transactions/{transactionId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTransaction(Guid typeId, Guid transactionId)
        {
            var (ok, error) = await _typeService.DeleteTransactionAsync(typeId, transactionId);
            return ok ? NoContent() : NotFound(new { error });
        }
    }
}
