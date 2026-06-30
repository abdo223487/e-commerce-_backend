using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    /// <summary>
    /// "Super-secret type" — a named bucket that transactions belong to.
    /// Created with a name only; transactions are then attached to it.
    /// </summary>
    public class TransactionType
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
