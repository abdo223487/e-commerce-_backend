using System.ComponentModel.DataAnnotations;
using MarketplaceApi.Models;

namespace MarketplaceApi.DTOs
{
    public class CreateOrderItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(1, 1000)]
        public int Quantity { get; set; } = 1;
    }

    public class CreateOrderDto
    {
        [Required, MinLength(1, ErrorMessage = "Order must contain at least one product.")]
        public List<CreateOrderItemDto> Items { get; set; } = new();

        [Required, MaxLength(300)]
        public string DeliveryAddress { get; set; } = string.Empty;
    }

    public class UpdateOrderStatusDto
    {
        [Required]
        public OrderStatus Status { get; set; }

        /// <summary>Optional note from the admin/supervisor explaining this transition (e.g. "Out for delivery with Mohamed, car 123").</summary>
        [MaxLength(500)]
        public string? Note { get; set; }
    }

    /// <summary>User-facing timeline entry: status + when + optional note, no internal actor identity.</summary>
    public class OrderTimelineEntryDto
    {
        public OrderStatus FromStatus { get; set; }
        public OrderStatus ToStatus { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>Admin/Supervisor-facing timeline entry: same as above plus who made the change.</summary>
    public class AdminOrderTimelineEntryDto : OrderTimelineEntryDto
    {
        public Guid? ChangedByUserId { get; set; }
        public string? ChangedByName { get; set; }
        public string? ChangedByRole { get; set; }
    }

    public class OrderItemResponseDto
    {
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
        public decimal CoinsPerUnit { get; set; }
        public decimal CoinsEarned => CoinsPerUnit * Quantity;
    }

    public class OrderResponseDto
    {
        public Guid Id { get; set; }
        public OrderStatus Status { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new();
        public List<OrderTimelineEntryDto> Timeline { get; set; } = new();
    }

    public class AdminOrderResponseDto : OrderResponseDto
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserPhone { get; set; }
        public new List<AdminOrderTimelineEntryDto> Timeline { get; set; } = new();
    }
}
