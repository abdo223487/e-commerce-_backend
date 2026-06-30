using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.DTOs
{
    public class CreateCategoryDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(1000), Url(ErrorMessage = "ImageUrl must be a valid URL.")]
        public string? ImageUrl { get; set; }
    }

    public class UpdateCategoryDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(1000), Url(ErrorMessage = "ImageUrl must be a valid URL.")]
        public string? ImageUrl { get; set; }
    }

    public class CategoryResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int ProductCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
