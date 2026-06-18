// ================================================================
// EcomPlatform.API/Controllers/StoreController.cs
// ================================================================
using Asp.Versioning;
using EcomPlatform.Application.DTOs.Store;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/store")]
    [AllowAnonymous]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public StoreController(IStoreService storeService)
        {
            _storeService = storeService;
        }

        // POST /api/v1/store/register
        [HttpPost("register")]
        [EnableRateLimiting("login")] // ✅ [1] نفس policy الـ auth — يحمي من abuse
        public async Task<IActionResult> RegisterStore([FromBody] RegisterStoreDto dto)
        {
            var result = await _storeService.RegisterStoreAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/v1/store/check-slug?slug=my-store
        [HttpGet("check-slug")]
        public async Task<IActionResult> CheckSlug([FromQuery] string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return BadRequest(new { message = "Slug is required" });

            var result = await _storeService.CheckSlugAvailabilityAsync(slug);
            return Ok(result);
        }
    }
}