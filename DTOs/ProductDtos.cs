using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.DTOs
{
    public class CreateProductDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(1000), Url(ErrorMessage = "ImageUrl must be a valid URL.")]
        public string ImageUrl { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CoinsPerUnit { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "StockQuantity must be 0 or greater.")]
        public int StockQuantity { get; set; } = 0;
    }

    public class UpdateProductDto : CreateProductDto
    {
    }

    /// <summary>Admin: adjust stock independently (restock / correction), without touching price/name.</summary>
    public class AdjustStockDto
    {
        /// <summary>
        /// Positive to add stock (restock), negative to remove stock
        /// (e.g. damaged goods). The resulting stock can never go below 0.
        /// </summary>
        [Required]
        public int Delta { get; set; }

        [MaxLength(300)]
        public string? Reason { get; set; }
    }

    public class ProductResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public decimal CoinsPerUnit { get; set; }
        public int StockQuantity { get; set; }
        public bool InStock => StockQuantity > 0;
        public Guid CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
