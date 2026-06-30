using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface IProductService
    {
        Task<List<ProductResponseDto>> GetAllAsync(Guid? categoryId = null);
        Task<ProductResponseDto?> GetByIdAsync(Guid id);
        Task<(bool ok, string? error, ProductResponseDto? data)> CreateAsync(Guid categoryId, CreateProductDto dto, Guid actorId);
        Task<(bool ok, string? error, ProductResponseDto? data)> UpdateAsync(Guid categoryId, Guid productId, UpdateProductDto dto, Guid actorId);
        Task<(bool ok, string? error)> DeleteAsync(Guid categoryId, Guid productId, Guid actorId);
        Task<(bool ok, string? error, ProductResponseDto? data)> AdjustStockAsync(Guid productId, AdjustStockDto dto, Guid actorId);
    }

    public class ProductService : IProductService
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;

        public ProductService(AppDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private static ProductResponseDto Map(Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            ImageUrl = p.ImageUrl,
            Price = p.Price,
            Description = p.Description,
            CoinsPerUnit = p.CoinsPerUnit,
            StockQuantity = p.StockQuantity,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name,
            CreatedAtUtc = p.CreatedAtUtc
        };

        public async Task<List<ProductResponseDto>> GetAllAsync(Guid? categoryId = null)
        {
            var query = _db.Products.Include(p => p.Category).AsQueryable();
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);

            var products = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
            return products.Select(Map).ToList();
        }

        public async Task<ProductResponseDto?> GetByIdAsync(Guid id)
        {
            var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
            return product is null ? null : Map(product);
        }

        public async Task<(bool ok, string? error, ProductResponseDto? data)> CreateAsync(Guid categoryId, CreateProductDto dto, Guid actorId)
        {
            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId);
            if (!categoryExists) return (false, "Category not found.", null);

            var product = new Product
            {
                Name = dto.Name.Trim(),
                ImageUrl = dto.ImageUrl.Trim(),
                Price = dto.Price,
                Description = dto.Description?.Trim(),
                CoinsPerUnit = dto.CoinsPerUnit,
                StockQuantity = dto.StockQuantity,
                CategoryId = categoryId
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            await _db.Entry(product).Reference(p => p.Category).LoadAsync();

            await _audit.LogAsync(actorId, AuditAction.ProductAdded,
                $"Product '{product.Name}' added at price {product.Price:F2} EGP in category {categoryId}",
                product.Id, "Product",
                after: new { product.Name, product.Price, product.CategoryId });

            return (true, null, Map(product));
        }

        public async Task<(bool ok, string? error, ProductResponseDto? data)> UpdateAsync(Guid categoryId, Guid productId, UpdateProductDto dto, Guid actorId)
        {
            var product = await _db.Products.Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == productId && p.CategoryId == categoryId);

            if (product is null) return (false, "Product not found in this category.", null);

            var before = new { product.Name, product.Price, product.Description };
            var oldPrice = product.Price;

            product.Name = dto.Name.Trim();
            product.ImageUrl = dto.ImageUrl.Trim();
            product.Price = dto.Price;
            product.Description = dto.Description?.Trim();
            product.CoinsPerUnit = dto.CoinsPerUnit;
            // NOTE: StockQuantity is intentionally NOT updated here. This
            // method loads the product, edits fields, then SaveChanges —
            // if it also wrote StockQuantity, a concurrent order that
            // atomically decremented stock between this read and this
            // write would be silently overwritten (lost update). Stock
            // must only ever change through AdjustStockAsync (admin) or
            // the atomic SQL paths in OrderService.

            await _db.SaveChangesAsync();

            var action = oldPrice != dto.Price ? AuditAction.PriceChanged : AuditAction.ProductUpdated;
            await _audit.LogAsync(actorId, action,
                oldPrice != dto.Price
                    ? $"Product '{product.Name}' price changed from {oldPrice:F2} to {dto.Price:F2} EGP"
                    : $"Product '{product.Name}' updated",
                productId, "Product",
                before: before,
                after: new { product.Name, product.Price, product.Description });

            return (true, null, Map(product));
        }

        public async Task<(bool ok, string? error)> DeleteAsync(Guid categoryId, Guid productId, Guid actorId)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.CategoryId == categoryId);
            if (product is null) return (false, "Product not found in this category.");

            var snapshot = new { product.Name, product.Price, product.CategoryId };
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            await _audit.LogAsync(actorId, AuditAction.ProductDeleted,
                $"Product '{product.Name}' deleted",
                productId, "Product", before: snapshot);

            return (true, null);
        }

        /// <summary>
        /// Admin-only restock / correction path. Applies the delta with a
        /// single atomic UPDATE (same technique used by OrderService),
        /// clamped at 0 on the DB side, so it can never race with a
        /// concurrent order decrement and never produce a negative stock.
        /// </summary>
        public async Task<(bool ok, string? error, ProductResponseDto? data)> AdjustStockAsync(Guid productId, AdjustStockDto dto, Guid actorId)
        {
            var exists = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId);
            if (!exists) return (false, "Product not found.", null);

            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Products""
                SET ""StockQuantity"" = GREATEST(""StockQuantity"" + {dto.Delta}, 0)
                WHERE ""Id"" = {productId}");

            if (affected == 0) return (false, "Product not found.", null);

            var product = await _db.Products.Include(p => p.Category).FirstAsync(p => p.Id == productId);

            await _audit.LogAsync(actorId, AuditAction.ProductUpdated,
                $"Stock for '{product.Name}' adjusted by {dto.Delta:+#;-#;0}, new stock: {product.StockQuantity}" +
                (string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" — reason: {dto.Reason}"),
                productId, "Product",
                after: new { product.StockQuantity });

            return (true, null, Map(product));
        }
    }
}
