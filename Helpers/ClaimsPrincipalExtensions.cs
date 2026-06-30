using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MarketplaceApi.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }

        public static string GetUserName(this ClaimsPrincipal user) =>
            user.FindFirst("name")?.Value ?? string.Empty;

        public static bool IsAdmin(this ClaimsPrincipal user) =>
            user.FindFirst("role")?.Value is "Admin" or "Supervisor";

        public static bool IsSupervisor(this ClaimsPrincipal user) =>
            user.FindFirst("role")?.Value == "Supervisor";

        public static bool IsRegularUser(this ClaimsPrincipal user) =>
            user.FindFirst("role")?.Value == "User";

        public static string GetRole(this ClaimsPrincipal user) =>
            user.FindFirst("role")?.Value ?? "User";
    }
}
