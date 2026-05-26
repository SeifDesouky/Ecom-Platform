// ================================================================
// EcomPlatform.API/Controllers/ProfileController.cs
// ================================================================
using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Profile;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IUserProfileService _profileService;

        public ProfileController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        // ── User endpoints (self) ─────────────────────────────────────────

        /// <summary>
        /// GET /api/v1/profile/me
        /// اليوزر يجيب بروفايله هو
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _profileService.GetProfileAsync(userId.Value);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// PUT /api/v1/profile/me
        /// اليوزر يعدل بروفايله هو — email و role محميين
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _profileService.UpdateMyProfileAsync(userId.Value, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Admin endpoints ───────────────────────────────────────────────

        /// <summary>
        /// GET /api/v1/profile/{userId}
        /// Admin يجيب بروفايل أي يوزر
        /// </summary>
        [HttpGet("{userId:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetProfileById(Guid userId)
        {
            var result = await _profileService.GetProfileAsync(userId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// PUT /api/v1/profile/{userId}
        /// Admin يعدل بروفايل أي يوزر (+ role, email, isActive)
        /// </summary>
        [HttpPut("{userId:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> AdminUpdateProfile(Guid userId, [FromBody] AdminUpdateProfileDto dto)
        {
            var result = await _profileService.AdminUpdateProfileAsync(userId, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Helper ────────────────────────────────────────────────────────

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}