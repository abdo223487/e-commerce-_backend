using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Models;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    [Produces("application/json")]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProfileController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>User: returns name, phone, coins, last order summary, and last delivery address.</summary>
        [HttpGet("me")]
        [Authorize(Policy = "UserOnly")]
        [ProducesResponseType(typeof(UserProfileResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Me()
        {
            var userId = User.GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound(new { error = "User not found." });

            var lastOrder = await _db.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Select(o => new LastOrderSummaryDto
                {
                    Id = o.Id,
                    Status = o.Status,
                    TotalPrice = o.TotalPrice,
                    DeliveryAddress = o.DeliveryAddress,
                    CreatedAtUtc = o.CreatedAtUtc
                })
                .FirstOrDefaultAsync();

            return Ok(new UserProfileResponseDto
            {
                Name = user.FullName,
                Phone = user.PhoneNumber,
                Coins = user.Coins,
                LastDeliveryAddress = lastOrder?.DeliveryAddress,
                LastOrder = lastOrder
            });
        }

        /// <summary>Admin/Supervisor: returns name, phone, total revenue from delivered orders, top 5 products, top 5 categories.</summary>
        [HttpGet("me/admin")]
        [Authorize(Policy = "AdminOrSupervisor")]
        [ProducesResponseType(typeof(AdminProfileResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MeAdmin()
        {
            var userId = User.GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound(new { error = "User not found." });

            var deliveredItems = await _db.OrderItems
                .Include(i => i.Order)
                .Include(i => i.Product)
                .ThenInclude(p => p!.Category)
                .Where(i => i.Order!.Status == OrderStatus.Delivered)
                .ToListAsync();

            var totalRevenue = deliveredItems.Sum(i => i.UnitPriceSnapshot * i.Quantity);

            var topProducts = deliveredItems
                .GroupBy(i => new { i.ProductId, Name = i.ProductNameSnapshot })
                .Select(g => new TopProductDto
                {
                    Id = g.Key.ProductId ?? Guid.Empty,
                    Name = g.Key.Name,
                    TotalUnitsSold = g.Sum(i => i.Quantity),
                    TotalRevenue = g.Sum(i => i.UnitPriceSnapshot * i.Quantity)
                })
                .OrderByDescending(p => p.TotalUnitsSold)
                .Take(5)
                .ToList();

            var topCategories = deliveredItems
                .GroupBy(i => new
                {
                    Id = i.CategoryIdSnapshot,
                    Name = i.Product?.Category?.Name ?? "Unknown"
                })
                .Select(g => new TopCategoryDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Name,
                    TotalUnitsSold = g.Sum(i => i.Quantity),
                    TotalRevenue = g.Sum(i => i.UnitPriceSnapshot * i.Quantity)
                })
                .OrderByDescending(c => c.TotalUnitsSold)
                .Take(5)
                .ToList();

            return Ok(new AdminProfileResponseDto
            {
                Name = user.FullName,
                Phone = user.PhoneNumber,
                TotalRevenue = totalRevenue,
                TopProducts = topProducts,
                TopCategories = topCategories
            });
        }

        /// <summary>Update name and phone. Available for all roles.</summary>
        [HttpPut("me")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateUserProfileDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var userId = User.GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound(new { error = "User not found." });

            var phoneTaken = await _db.Users
                .AnyAsync(u => u.PhoneNumber == dto.Phone && u.Id != userId);
            if (phoneTaken)
                return BadRequest(new { error = "Phone number is already in use by another account." });

            user.FullName = dto.Name.Trim();
            user.PhoneNumber = dto.Phone.Trim();
            await _db.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully.", name = user.FullName, phone = user.PhoneNumber });
        }
    }
}
