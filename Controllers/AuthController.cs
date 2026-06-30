using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketplaceApi.DTOs;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting("auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Register a new user or admin account.
        /// If the password contains "@admi" the account becomes an Admin (IsUser = false), otherwise a regular User.
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var result = await _authService.RegisterAsync(dto);
            if (!result.Success) return BadRequest(new { error = result.Error });

            return Created(string.Empty, result.Data);
        }

        /// <summary>
        /// Login with phone + password. Works for both users and admins.
        /// The resulting token's IsUser claim indicates the account type.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var result = await _authService.LoginAsync(dto);
            if (!result.Success) return Unauthorized(new { error = result.Error });

            return Ok(result.Data);
        }

        /// <summary>
        /// Exchange a valid refresh token for a new access + refresh token pair.
        /// Use this when the 5-minute access token expires, instead of forcing a re-login.
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var result = await _authService.RefreshAsync(dto.RefreshToken);
            if (!result.Success) return Unauthorized(new { error = result.Error });

            return Ok(result.Data);
        }

        /// <summary>
        /// Revoke a refresh token so it can never be used again.
        /// The short-lived access token will expire naturally on its own.
        /// </summary>
        [HttpPost("revoke")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenRequestDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var (ok, error) = await _authService.RevokeAsync(dto.RefreshToken);
            return ok ? NoContent() : BadRequest(new { error });
        }
    }
}
