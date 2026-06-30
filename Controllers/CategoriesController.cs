using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [Produces("application/json")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<CategoryResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll() => Ok(await _categoryService.GetAllAsync());

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            return category is null ? NotFound(new { error = "Category not found." }) : Ok(category);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _categoryService.CreateAsync(dto, User.GetUserId());
            return ok ? CreatedAtAction(nameof(GetById), new { id = data!.Id }, data) : BadRequest(new { error });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error, data) = await _categoryService.UpdateAsync(id, dto, User.GetUserId());
            return ok ? Ok(data) : NotFound(new { error });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (ok, error) = await _categoryService.DeleteAsync(id, User.GetUserId());
            return ok ? NoContent() : NotFound(new { error });
        }
    }
}
