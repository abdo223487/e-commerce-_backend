using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public AuthResponseDto? Data { get; set; }

        public static AuthResult Fail(string error) => new() { Success = false, Error = error };
        public static AuthResult Ok(AuthResponseDto data) => new() { Success = true, Data = data };
    }

    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterUserDto dto);
        Task<AuthResult> LoginAsync(LoginDto dto);
        Task<AuthResult> RefreshAsync(string refreshToken);
        Task<(bool ok, string? error)> RevokeAsync(string refreshToken);
    }

    public class AuthService : IAuthService
    {
        private const string AdminPasswordMarker = "@admi";
        private const string SupervisorPasswordMarker = "@super";
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly ITokenService _tokenService;

        public AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokenService)
        {
            _db = db;
            _hasher = hasher;
            _tokenService = tokenService;
        }

        private async Task<AuthResponseDto> IssueTokensAsync(User user)
        {
            var (accessToken, accessExpires) = _tokenService.GenerateToken(user);
            var (refreshToken, refreshExpires) = _tokenService.GenerateRefreshToken();

            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAtUtc = refreshExpires
            });

            await _db.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = accessToken,
                ExpiresAtUtc = accessExpires,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = refreshExpires,
                IsUser = user.Role == UserRole.User,
                Role = user.Role.ToString(),
                FullName = user.FullName
            };
        }

        public async Task<AuthResult> RegisterAsync(RegisterUserDto dto)
        {
            // Role is decided by password content:
            // @super => Supervisor, @admi => Admin, otherwise => User
            UserRole role;
            if (dto.Password.Contains(SupervisorPasswordMarker, StringComparison.Ordinal))
                role = UserRole.Supervisor;
            else if (dto.Password.Contains(AdminPasswordMarker, StringComparison.Ordinal))
                role = UserRole.Admin;
            else
                role = UserRole.User;

            var phoneExists = await _db.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber);
            if (phoneExists)
                return AuthResult.Fail("Phone number already registered.");

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                PasswordHash = _hasher.Hash(dto.Password),
                Role = role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return AuthResult.Ok(await IssueTokensAsync(user));
        }

        public async Task<AuthResult> LoginAsync(LoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber.Trim());

            if (user is null)
            {
                _hasher.Hash(dto.Password);
                return AuthResult.Fail("Invalid phone number or password.");
            }

            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc > DateTime.UtcNow)
                return AuthResult.Fail($"Account is temporarily locked. Try again after {user.LockoutEndUtc:O}.");

            var validPassword = _hasher.Verify(dto.Password, user.PasswordHash);

            if (!validPassword)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                    user.FailedLoginAttempts = 0;
                }
                await _db.SaveChangesAsync();
                return AuthResult.Fail("Invalid phone number or password.");
            }

            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            await _db.SaveChangesAsync();

            return AuthResult.Ok(await IssueTokensAsync(user));
        }

        public async Task<AuthResult> RefreshAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return AuthResult.Fail("Refresh token is required.");

            var stored = await _db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (stored is null)
                return AuthResult.Fail("Invalid refresh token.");

            if (stored.RevokedAtUtc is not null)
            {
                var activeTokens = await _db.RefreshTokens
                    .Where(rt => rt.UserId == stored.UserId && rt.RevokedAtUtc == null)
                    .ToListAsync();

                foreach (var t in activeTokens) t.RevokedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                return AuthResult.Fail("Refresh token has already been used. All sessions were revoked for safety; please log in again.");
            }

            if (DateTime.UtcNow >= stored.ExpiresAtUtc)
                return AuthResult.Fail("Refresh token has expired. Please log in again.");

            var user = stored.User;
            if (user is null) return AuthResult.Fail("Invalid refresh token.");

            var (newAccessToken, accessExpires) = _tokenService.GenerateToken(user);
            var (newRefreshToken, refreshExpires) = _tokenService.GenerateRefreshToken();

            stored.RevokedAtUtc = DateTime.UtcNow;
            stored.ReplacedByToken = newRefreshToken;

            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = newRefreshToken,
                UserId = user.Id,
                ExpiresAtUtc = refreshExpires
            });

            await _db.SaveChangesAsync();

            return AuthResult.Ok(new AuthResponseDto
            {
                Token = newAccessToken,
                ExpiresAtUtc = accessExpires,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiresAtUtc = refreshExpires,
                IsUser = user.Role == UserRole.User,
                Role = user.Role.ToString(),
                FullName = user.FullName
            });
        }

        public async Task<(bool ok, string? error)> RevokeAsync(string refreshToken)
        {
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken);
            if (stored is null) return (false, "Invalid refresh token.");

            if (stored.RevokedAtUtc is null)
            {
                stored.RevokedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return (true, null);
        }
    }
}
