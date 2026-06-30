using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;

namespace MarketplaceApi.Services
{
    public interface IRecommendationService
    {
        Task<List<ProductResponseDto>> GetRecommendationsAsync(Guid userId, int maxResults = 12);
    }

    /// <summary>
    /// Recommends more products from whichever categories the user bought from
    /// most in their last two orders. Falls back to newest products overall
    /// for users with no order history yet.
    /// </summary>
    public class RecommendationService : IRecommendationService
    {
        private readonly AppDbContext _db;

        public RecommendationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<ProductResponseDto>> GetRecommendationsAsync(Guid userId, int maxResults = 12)
        {
            var lastTwoOrderIds = await _db.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Take(2)
                .Select(o => o.Id)
                .ToListAsync();

            List<Guid> alreadyPurchasedProductIds = new();
            List<Guid> topCategoryIds;

            if (lastTwoOrderIds.Count == 0)
            {
                // No history yet -> just return newest products as a sensible default.
                var newest = await _db.Products
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Take(maxResults)
                    .ToListAsync();

                return newest.Select(Map).ToList();
            }

            var itemsFromLastTwoOrders = await _db.OrderItems
                .Where(i => lastTwoOrderIds.Contains(i.OrderId))
                .ToListAsync();

            alreadyPurchasedProductIds = itemsFromLastTwoOrders
                .Where(i => i.ProductId.HasValue)
                .Select(i => i.ProductId!.Value)
                .Distinct()
                .ToList();

            // Rank categories by how many units were bought from them across the last 2 orders.
            topCategoryIds = itemsFromLastTwoOrders
                .GroupBy(i => i.CategoryIdSnapshot)
                .OrderByDescending(g => g.Sum(i => i.Quantity))
                .Select(g => g.Key)
                .ToList();

            if (topCategoryIds.Count == 0)
            {
                var newest = await _db.Products
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Take(maxResults)
                    .ToListAsync();

                return newest.Select(Map).ToList();
            }

            var candidates = await _db.Products
                .Include(p => p.Category)
                .Where(p => topCategoryIds.Contains(p.CategoryId))
                .ToListAsync();

            // Order candidates: prefer products from the #1 category, then #2, etc.,
            // skip ones the user already bought recently, newest first within each category.
            var ordered = candidates
                .Where(p => !alreadyPurchasedProductIds.Contains(p.Id))
                .OrderBy(p => topCategoryIds.IndexOf(p.CategoryId))
                .ThenByDescending(p => p.CreatedAtUtc)
                .Take(maxResults)
                .ToList();

            // If we don't have enough (e.g. small catalog), top up with already-purchased items too.
            if (ordered.Count < maxResults)
            {
                var fillers = candidates
                    .Except(ordered)
                    .OrderBy(p => topCategoryIds.IndexOf(p.CategoryId))
                    .ThenByDescending(p => p.CreatedAtUtc)
                    .Take(maxResults - ordered.Count);

                ordered.AddRange(fillers);
            }

            return ordered.Select(Map).ToList();
        }

        private static ProductResponseDto Map(Models.Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            ImageUrl = p.ImageUrl,
            Price = p.Price,
            Description = p.Description,
            CoinsPerUnit = p.CoinsPerUnit,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name,
            CreatedAtUtc = p.CreatedAtUtc
        };
    }
}
