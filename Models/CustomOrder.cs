using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    public enum CustomOrderStatus
    {
        Open = 0,
        InProgress = 1,
        Closed = 2
    }

    /// <summary>
    /// A free-form / custom order request: the user names what they want,
    /// then exchanges text messages with admins to clarify details.
    /// </summary>
    public class CustomOrder
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        public CustomOrderStatus Status { get; set; } = CustomOrderStatus.Open;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public List<CustomOrderMessage> Messages { get; set; } = new();
    }

    public class CustomOrderMessage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CustomOrderId { get; set; }
        public CustomOrder? CustomOrder { get; set; }

        public Guid SenderId { get; set; }

        [MaxLength(100)]
        public string SenderName { get; set; } = string.Empty;

        public bool SenderIsAdmin { get; set; }

        [Required, MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
