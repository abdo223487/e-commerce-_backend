using System.ComponentModel.DataAnnotations;

namespace MarketplaceApi.DTOs
{
    public class RegisterUserDto
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Invalid Egyptian phone number format.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiresAtUtc { get; set; }
        public bool IsUser { get; set; }
        public string Role { get; set; } = "User";
        public string FullName { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
