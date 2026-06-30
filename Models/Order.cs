using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketplaceApi.Models
{
    public enum OrderStatus
    {
        Pending = 0,
        Confirmed = 1,
        Preparing = 2,
        Shipped = 3,
        Delivered = 4,
        Cancelled = 5
    }

    public class Order
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Required, MaxLength(300)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Guard flag so coins are only ever credited once, when the order
        /// transitions into Delivered for the first time.
        /// </summary>
        public bool CoinsAwarded { get; set; } = false;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public List<OrderItem> Items { get; set; } = new();

        public List<OrderStatusHistory> StatusHistory { get; set; } = new();
    }

    public class OrderItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid CategoryIdSnapshot { get; set; }

        // Snapshots taken at order time, so later price/coin changes on the
        // product don't retroactively change historical orders.
        [MaxLength(150)]
        public string ProductNameSnapshot { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPriceSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CoinsPerUnitSnapshot { get; set; }

        public int Quantity { get; set; }
    }
}
