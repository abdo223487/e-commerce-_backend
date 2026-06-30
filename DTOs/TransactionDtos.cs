using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.DTOs
{
    public class CreateTransactionDto
    {
        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }

        [Required]
        [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Invalid Egyptian phone number format.")]
        public string PhoneNumber { get; set; } = string.Empty;

        public Guid TypeId { get; set; }
    }

    public class UpdateTransactionDto
    {
        [MaxLength(500)]
        public string? Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal? Price { get; set; }

        [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Invalid Egyptian phone number format.")]
        public string? PhoneNumber { get; set; }

        public Guid? TypeId { get; set; }
    }

    public class TransactionResponseDto
    {
        public Guid Id { get; set; }
        public Guid SupervisorId { get; set; }
        public string SupervisorName { get; set; } = string.Empty;
        public Guid TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
