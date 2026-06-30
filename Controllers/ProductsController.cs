using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/categories/{categoryId:guid}/products")]
    [Produces("application/json")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(Guid categoryId) => Ok(await _productService.GetAllAsync(categoryId));

        [HttpPost]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(Guid categoryId, [FromBody] CreateProductDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _productService.CreateAsync(categoryId, dto, User.GetUserId());
            return ok ? CreatedAtAction(nameof(ProductsLookupController.GetById),
                "ProductsLookup", new { id = data!.Id }, data) : BadRequest(new { error });
        }

        [HttpPut("{productId:guid}")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid categoryId, Guid productId, [FromBody] UpdateProductDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _productService.UpdateAsync(categoryId, productId, dto, User.GetUserId());
            return ok ? Ok(data) : NotFound(new { error });
        }

        [HttpDelete("{productId:guid}")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid categoryId, Guid productId)
        {
            var (ok, error) = await _productService.DeleteAsync(categoryId, productId, User.GetUserId());
            return ok ? NoContent() : NotFound(new { error });
        }

        /// <summary>Admin/Supervisor: restock or correct stock (atomic, race-safe). Positive delta adds stock, negative removes.</summary>
        [HttpPatch("{productId:guid}/stock")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdjustStock(Guid categoryId, Guid productId, [FromBody] AdjustStockDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _productService.AdjustStockAsync(productId, dto, User.GetUserId());
            return ok ? Ok(data) : NotFound(new { error });
        }
    }

    [ApiController]
    [Route("api/products")]
    [Produces("application/json")]
    public class ProductsLookupController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsLookupController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] Guid? categoryId) =>
            Ok(await _productService.GetAllAsync(categoryId));

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _productService.GetByIdAsync(id);
            return product is null ? NotFound(new { error = "Product not found." }) : Ok(product);
        }
    }
}
