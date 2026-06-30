using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.DTOs
{
    public class CreateTransactionTypeDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateTransactionTypeDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
    }

    public class TransactionTypeResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TransactionsCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
