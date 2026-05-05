using EcomPlatform.Application.DTOs.Coupons;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponsController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _couponService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _couponService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCouponDto dto)
        {
            var result = await _couponService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _couponService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _couponService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> Validate([FromBody] ValidateCouponDto dto)
        {
            var result = await _couponService.ValidateAsync(dto);
            return Ok(result);
        }
    }
}