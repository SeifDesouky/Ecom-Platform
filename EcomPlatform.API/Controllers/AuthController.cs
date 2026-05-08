// ================================================================
// EcomPlatform.API/Controllers/AuthController.cs — UPDATED
// ================================================================
using Asp.Versioning;
using EcomPlatform.Application.DTOs.Auth;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // ── POST /api/v1/auth/register ────────────────────────────────────
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── POST /api/v1/auth/login ───────────────────────────────────────
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var ip = GetClientIp();
            var device = Request.Headers.UserAgent.ToString();

            var result = await _authService.LoginAsync(dto, ip, device);
            return result.Success ? Ok(result) : Unauthorized(result);
        }

        // ── POST /api/v1/auth/refresh ─────────────────────────────────────
        /// <summary>
        /// تجديد الـ Access Token باستخدام Refresh Token
        /// بيتم Token Rotation تلقائياً
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            var ip = GetClientIp();
            var device = Request.Headers.UserAgent.ToString();

            var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ip, device);
            return result.Success ? Ok(result) : Unauthorized(result);
        }

        // ── POST /api/v1/auth/revoke ──────────────────────────────────────
        /// <summary>
        /// Logout من الـ device الحالي
        /// </summary>
        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequestDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _authService.RevokeTokenAsync(dto.RefreshToken, userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── POST /api/v1/auth/revoke-all ──────────────────────────────────
        /// <summary>
        /// Logout من كل الأجهزة — بيلغي جميع الـ Refresh Tokens
        /// </summary>
        [HttpPost("revoke-all")]
        [Authorize]
        public async Task<IActionResult> RevokeAll()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _authService.RevokeAllTokensAsync(userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── GET /api/v1/auth/sessions ─────────────────────────────────────
        /// <summary>
        /// عرض كل الـ active sessions للـ user الحالي
        /// </summary>
        [HttpGet("sessions")]
        [Authorize]
        public async Task<IActionResult> GetSessions()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _authService.GetActiveSessionsAsync(userId.Value);
            return Ok(result);
        }

        // ── DELETE /api/v1/auth/sessions/{tokenId} ────────────────────────
        /// <summary>
        /// إلغاء session معينة بالـ TokenId (للـ UI اللي بيعرض الأجهزة)
        /// </summary>
        [HttpDelete("sessions/{tokenId:guid}")]
        [Authorize]
        public async Task<IActionResult> RevokeSession(Guid tokenId)
        {
            // هنا ممكن تعمل RevokeByTokenId لو عايز —
            // للبساطة دلوقتي بيطلب الـ plain token من الـ body
            return Ok(new { message = "Use POST /revoke with the token value" });
        }

        // ── Private Helpers ───────────────────────────────────────────────

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private string? GetClientIp()
        {
            // Support for X-Forwarded-For (behind proxy/load balancer)
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',').First().Trim();

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
