using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketplaceApi.Models
{
    public class Product
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string ImageUrl { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// Coins a user earns per unit purchased of this product, set by the admin.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CoinsPerUnit { get; set; } = 0;

        /// <summary>
        /// Available stock for this product. Decremented atomically (at the
        /// database level, via a conditional UPDATE) whenever an order is
        /// created, and restored atomically whenever an order is cancelled
        /// or a pending order is deleted. Never read-modify-written through
        /// EF Core change tracking, to avoid lost-update race conditions
        /// when two orders for the same product arrive at the same time.
        /// </summary>
        public int StockQuantity { get; set; } = 0;

        public Guid CategoryId { get; set; }
        public Category? Category { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
