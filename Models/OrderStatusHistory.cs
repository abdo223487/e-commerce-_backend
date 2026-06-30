using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    /// <summary>
    /// One immutable row per status transition an order goes through.
    /// This is the backbone of order tracking: the user sees a clean
    /// timeline ("Pending -> Confirmed -> Preparing -> ..."), and the
    /// admin/supervisor additionally sees who made each change and any
    /// note they left.
    /// </summary>
    public class OrderStatusHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public OrderStatus FromStatus { get; set; }
        public OrderStatus ToStatus { get; set; }

        /// <summary>
        /// Who triggered this transition. Null for the very first row,
        /// which is created automatically by the system when the order
        /// is placed.
        /// </summary>
        public Guid? ChangedByUserId { get; set; }
        public User? ChangedByUser { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
