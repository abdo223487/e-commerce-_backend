using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    public enum AuditAction
    {
        OrderDeleted = 0,
        OrderStatusChanged = 1,
        ProductAdded = 2,
        ProductUpdated = 3,
        ProductDeleted = 4,
        CategoryAdded = 5,
        CategoryUpdated = 6,
        CategoryDeleted = 7,
        OrderCreated = 8,
        PriceChanged = 9
    }

    public class AuditLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ActorId { get; set; }
        public User? Actor { get; set; }

        public AuditAction Action { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>JSON snapshot of relevant entity before change.</summary>
        public string? BeforeSnapshot { get; set; }

        /// <summary>JSON snapshot of relevant entity after change.</summary>
        public string? AfterSnapshot { get; set; }

        public Guid? RelatedEntityId { get; set; }

        [MaxLength(100)]
        public string? RelatedEntityType { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
