using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    /// <summary>
    /// A general review about the store/place (not tied to a specific product).
    /// </summary>
    public class Review
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required, MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
