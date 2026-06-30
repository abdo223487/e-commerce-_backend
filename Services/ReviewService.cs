using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface IReviewService
    {
        Task<List<ReviewResponseDto>> GetAllAsync(bool isAdmin);
        Task<ReviewResponseDto?> GetByIdAsync(Guid id, bool isAdmin);
        Task<(bool ok, string? error, ReviewResponseDto? data)> CreateAsync(Guid userId, CreateReviewDto dto);
        Task<(bool ok, string? error, ReviewResponseDto? data)> UpdateAsync(Guid id, UpdateReviewDto dto, Guid? restrictToUserId);
        Task<(bool ok, string? error)> DeleteAsync(Guid id, Guid? restrictToUserId);
    }

    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _db;
        private readonly CoinsSettings _coinsSettings;

        public ReviewService(AppDbContext db, IOptions<CoinsSettings> coinsSettings)
        {
            _db = db;
            _coinsSettings = coinsSettings.Value;
        }

        private static ReviewResponseDto MapUser(Review r) => new()
        {
            Id = r.Id,
            UserName = r.User?.FullName,
            Rating = r.Rating,
            Content = r.Content,
            CreatedAtUtc = r.CreatedAtUtc,
            UpdatedAtUtc = r.UpdatedAtUtc
        };

        private static AdminReviewResponseDto MapAdmin(Review r) => new()
        {
            Id = r.Id,
            UserId = r.UserId,
            UserName = r.User?.FullName,
            Rating = r.Rating,
            Content = r.Content,
            CreatedAtUtc = r.CreatedAtUtc,
            UpdatedAtUtc = r.UpdatedAtUtc
        };

        public async Task<List<ReviewResponseDto>> GetAllAsync(bool isAdmin)
        {
            var reviews = await _db.Reviews.Include(r => r.User)
                .OrderByDescending(r => r.CreatedAtUtc).ToListAsync();

            return isAdmin
                ? reviews.Select(r => (ReviewResponseDto)MapAdmin(r)).ToList()
                : reviews.Select(MapUser).ToList();
        }

        public async Task<ReviewResponseDto?> GetByIdAsync(Guid id, bool isAdmin)
        {
            var review = await _db.Reviews.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
            if (review is null) return null;
            return isAdmin ? MapAdmin(review) : MapUser(review);
        }

        public async Task<(bool ok, string? error, ReviewResponseDto? data)> CreateAsync(Guid userId, CreateReviewDto dto)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return (false, "User not found.", null);

            var review = new Review
            {
                UserId = userId,
                Rating = dto.Rating,
                Content = dto.Content.Trim()
            };

            _db.Reviews.Add(review);
            user.Coins += _coinsSettings.ReviewCoins;
            await _db.SaveChangesAsync();

            review.User = user;
            return (true, null, MapUser(review));
        }

        public async Task<(bool ok, string? error, ReviewResponseDto? data)> UpdateAsync(Guid id, UpdateReviewDto dto, Guid? restrictToUserId)
        {
            var query = _db.Reviews.Include(r => r.User).Where(r => r.Id == id);
            if (restrictToUserId.HasValue) query = query.Where(r => r.UserId == restrictToUserId.Value);

            var review = await query.FirstOrDefaultAsync();
            if (review is null) return (false, "Review not found.", null);

            review.Rating = dto.Rating;
            review.Content = dto.Content.Trim();
            review.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            // return admin view only if no restriction (admin called it)
            return (true, null, restrictToUserId.HasValue ? MapUser(review) : MapAdmin(review));
        }

        public async Task<(bool ok, string? error)> DeleteAsync(Guid id, Guid? restrictToUserId)
        {
            var query = _db.Reviews.Where(r => r.Id == id);
            if (restrictToUserId.HasValue) query = query.Where(r => r.UserId == restrictToUserId.Value);

            var review = await query.FirstOrDefaultAsync();
            if (review is null) return (false, "Review not found.");

            _db.Reviews.Remove(review);
            await _db.SaveChangesAsync();
            return (true, null);
        }
    }
}
