using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Coupons;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponsController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        // Staff وفوق — يشوف coupons الـ tenant
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _couponService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // Staff وفوق — يشوف coupon معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _couponService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إنشاء coupon
        [HttpPost]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateCouponDto dto)
        {
            var result = await _couponService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف coupon
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _couponService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تفعيل/تعطيل coupon
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _couponService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // AllowAnonymous — الـ storefront يتحقق من الكوبون بدون login
        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> Validate([FromBody] ValidateCouponDto dto)
        {
            var result = await _couponService.ValidateAsync(dto);
            return Ok(result);
        }
    }
}