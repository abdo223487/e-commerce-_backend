using System.ComponentModel.DataAnnotations;
using MarketplaceApi.Models;

namespace MarketplaceApi.DTOs
{
    public class CreateCustomOrderDto
    {
        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? InitialMessage { get; set; }
    }

    public class CreateCustomOrderMessageDto
    {
        [Required, MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
    }

    public class UpdateCustomOrderStatusDto
    {
        [Required]
        public CustomOrderStatus Status { get; set; }
    }

    public class CustomOrderMessageResponseDto
    {
        public Guid Id { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool SenderIsAdmin { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    // Returned to the user (no userId/userName)
    public class CustomOrderResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public CustomOrderStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public List<CustomOrderMessageResponseDto> Messages { get; set; } = new();
    }

    // Returned to admin (includes user info)
    public class AdminCustomOrderResponseDto : CustomOrderResponseDto
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
    }
}
