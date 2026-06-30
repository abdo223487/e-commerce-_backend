using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.DTOs
{
    public class CreateReviewDto
    {
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required, MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }

    public class UpdateReviewDto
    {
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required, MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }

    // Returned to everyone (public reviews — no userId/userName needed for the owner)
    public class ReviewResponseDto
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }   // name only, no id
        public int Rating { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    // Admin-only: includes userId for management
    public class AdminReviewResponseDto : ReviewResponseDto
    {
        public Guid UserId { get; set; }
    }
}
