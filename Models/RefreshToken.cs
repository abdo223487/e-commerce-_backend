using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(200)]
        public string Token { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        /// <summary>If this token was rotated, points to the token that replaced it.</summary>
        [MaxLength(200)]
        public string? ReplacedByToken { get; set; }

        public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
    }
}
