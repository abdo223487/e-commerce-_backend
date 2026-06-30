using System.ComponentModel.DataAnnotations;
using MarketplaceApi.Models;

namespace MarketplaceApi.DTOs
{
    // ── User profile response ──────────────────────────────────────────────────
    public class UserProfileResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal Coins { get; set; }
        public string? LastDeliveryAddress { get; set; }
        public LastOrderSummaryDto? LastOrder { get; set; }
    }

    public class LastOrderSummaryDto
    {
        public Guid Id { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalPrice { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    // ── User profile update ────────────────────────────────────────────────────
    public class UpdateUserProfileDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Invalid Egyptian phone number format.")]
        public string Phone { get; set; } = string.Empty;
    }

    // ── Admin profile response ─────────────────────────────────────────────────
    public class AdminProfileResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public List<TopProductDto> TopProducts { get; set; } = new();
        public List<TopCategoryDto> TopCategories { get; set; } = new();
    }

    public class TopProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalUnitsSold { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class TopCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalUnitsSold { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
