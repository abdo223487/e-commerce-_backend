using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    public class Category
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? ImageUrl { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<Product> Products { get; set; } = new();
    }
}
