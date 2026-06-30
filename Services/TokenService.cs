using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MarketplaceApi.Helpers;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface ITokenService
    {
        (string token, DateTime expiresAtUtc) GenerateToken(User user);
        (string token, DateTime expiresAtUtc) GenerateRefreshToken();
    }

    public class TokenService : ITokenService
    {
        private readonly JwtSettings _settings;

        public TokenService(IOptions<JwtSettings> settings)
        {
            _settings = settings.Value;
        }

        public (string token, DateTime expiresAtUtc) GenerateToken(User user)
        {
            var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

            var roleString = user.Role switch
            {
                UserRole.Admin => "Admin",
                UserRole.Supervisor => "Supervisor",
                _ => "User"
            };

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("phone", user.PhoneNumber),
                new("name", user.FullName),
                new("IsUser", (user.Role == UserRole.User).ToString().ToLowerInvariant(), ClaimValueTypes.Boolean),
                new("role", roleString),
                new("securityStamp", user.SecurityStamp),
                new(ClaimTypes.Role, roleString)
            };

            var keyBytes = Encoding.UTF8.GetBytes(_settings.Key);
            var signingKey = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        public (string token, DateTime expiresAtUtc) GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            var token = Convert.ToBase64String(randomBytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            return (token, DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiryDays));
        }
    }
}
