using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketplaceApi.Models
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>The supervisor who created this transaction.</summary>
        public Guid SupervisorId { get; set; }
        public User? Supervisor { get; set; }

        /// <summary>The type ("super-secret type") this transaction belongs to. Required.</summary>
        public Guid TypeId { get; set; }
        public TransactionType? Type { get; set; }

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
