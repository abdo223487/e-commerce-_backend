using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.Models
{
    public enum UserRole
    {
        User = 0,
        Admin = 1,
        Supervisor = 2
    }

    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// User = 0 (normal user), Admin = 1 (admin), Supervisor = 2 (supervisor)
        /// </summary>
        public UserRole Role { get; set; } = UserRole.User;

        /// <summary>
        /// Legacy: true => User, false => Admin or Supervisor
        /// </summary>
        public bool IsUser => Role == UserRole.User;

        public decimal Coins { get; set; } = 0;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndUtc { get; set; }

        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    }
}
