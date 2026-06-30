using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface ICategoryService
    {
        Task<List<CategoryResponseDto>> GetAllAsync();
        Task<CategoryResponseDto?> GetByIdAsync(Guid id);
        Task<(bool ok, string? error, CategoryResponseDto? data)> CreateAsync(CreateCategoryDto dto, Guid actorId);
        Task<(bool ok, string? error, CategoryResponseDto? data)> UpdateAsync(Guid id, UpdateCategoryDto dto, Guid actorId);
        Task<(bool ok, string? error)> DeleteAsync(Guid id, Guid actorId);
    }

    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;

        public CategoryService(AppDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private static CategoryResponseDto Map(Category c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            ImageUrl = c.ImageUrl,
            ProductCount = c.Products?.Count ?? 0,
            CreatedAtUtc = c.CreatedAtUtc
        };

        public async Task<List<CategoryResponseDto>> GetAllAsync()
        {
            var categories = await _db.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return categories.Select(Map).ToList();
        }

        public async Task<CategoryResponseDto?> GetByIdAsync(Guid id)
        {
            var category = await _db.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            return category is null ? null : Map(category);
        }

        public async Task<(bool ok, string? error, CategoryResponseDto? data)> CreateAsync(CreateCategoryDto dto, Guid actorId)
        {
            var nameExists = await _db.Categories.AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());
            if (nameExists) return (false, "A category with this name already exists.", null);

            var category = new Category
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                ImageUrl = dto.ImageUrl?.Trim()
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            await _audit.LogAsync(actorId, AuditAction.CategoryAdded,
                $"Category '{category.Name}' created",
                category.Id, "Category",
                after: new { category.Name, category.Description });

            return (true, null, Map(category));
        }

        public async Task<(bool ok, string? error, CategoryResponseDto? data)> UpdateAsync(Guid id, UpdateCategoryDto dto, Guid actorId)
        {
            var category = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
            if (category is null) return (false, "Category not found.", null);

            var nameTaken = await _db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == dto.Name.Trim().ToLower());
            if (nameTaken) return (false, "Another category already uses this name.", null);

            var before = new { category.Name, category.Description };
            category.Name = dto.Name.Trim();
            category.Description = dto.Description?.Trim();
            category.ImageUrl = dto.ImageUrl?.Trim();
            await _db.SaveChangesAsync();

            await _audit.LogAsync(actorId, AuditAction.CategoryUpdated,
                $"Category updated to '{category.Name}'",
                id, "Category",
                before: before,
                after: new { category.Name, category.Description });

            return (true, null, Map(category));
        }

        public async Task<(bool ok, string? error)> DeleteAsync(Guid id, Guid actorId)
        {
            var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category is null) return (false, "Category not found.");

            var snapshot = new { category.Name };
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();

            await _audit.LogAsync(actorId, AuditAction.CategoryDeleted,
                $"Category '{category.Name}' deleted",
                id, "Category", before: snapshot);

            return (true, null);
        }
    }
}
