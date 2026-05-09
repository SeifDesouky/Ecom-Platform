using Asp.Versioning;
using EcomPlatform.Application.DTOs.Auth;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var ip = GetClientIp();
            var device = Request.Headers.UserAgent.ToString();
            var result = await _authService.LoginAsync(ip, device, dto);
            return result.Success ? Ok(result) : Unauthorized(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            var ip = GetClientIp();
            var device = Request.Headers.UserAgent.ToString();
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ip, device);
            return result.Success ? Ok(result) : Unauthorized(result);
        }

        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequestDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _authService.RevokeTokenAsync(dto.RefreshToken, userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("revoke-all")]
        [Authorize]
        public async Task<IActionResult> RevokeAll()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _authService.RevokeAllTokensAsync(userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("sessions")]
        [Authorize]
        public async Task<IActionResult> GetSessions()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _authService.GetActiveSessionsAsync(userId.Value);
            return Ok(result);
        }

        [HttpDelete("sessions/{tokenId:guid}")]
        [Authorize]
        public async Task<IActionResult> RevokeSession(Guid tokenId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _authService.RevokeTokenByIdAsync(tokenId, userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private string? GetClientIp()
        {
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',').First().Trim();
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}