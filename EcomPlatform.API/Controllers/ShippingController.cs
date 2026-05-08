using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Shipping;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ShippingController : ControllerBase
    {
        private readonly IShippingService _shippingService;

        public ShippingController(IShippingService shippingService)
        {
            _shippingService = shippingService;
        }

        // ─── Zones ───────────────────────────────────────────────────────────

        // Staff وفوق — يشوف الـ zones
        [HttpGet("zones/tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetZonesByTenant(Guid tenantId)
        {
            var result = await _shippingService.GetZonesByTenantAsync(tenantId);
            return Ok(result);
        }

        // Staff وفوق — يشوف zone معينة
        [HttpGet("zones/{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetZoneById(Guid id)
        {
            var result = await _shippingService.GetZoneByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إنشاء zone
        [HttpPost("zones")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateZone([FromBody] CreateShippingZoneDto dto)
        {
            var result = await _shippingService.CreateZoneAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف zone
        [HttpDelete("zones/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteZone(Guid id)
        {
            var result = await _shippingService.DeleteZoneAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تفعيل/تعطيل zone
        [HttpPatch("zones/{id}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleZoneStatus(Guid id)
        {
            var result = await _shippingService.ToggleZoneStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // ─── Methods ─────────────────────────────────────────────────────────

        // TenantAdmin وفوق — إنشاء shipping method
        [HttpPost("methods")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateMethod([FromBody] CreateShippingMethodDto dto)
        {
            var result = await _shippingService.CreateMethodAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف shipping method
        [HttpDelete("methods/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteMethod(Guid id)
        {
            var result = await _shippingService.DeleteMethodAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تفعيل/تعطيل method
        [HttpPatch("methods/{id}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleMethodStatus(Guid id)
        {
            var result = await _shippingService.ToggleMethodStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // ─── Calculate ───────────────────────────────────────────────────────

        // AllowAnonymous — الـ storefront يحسب الشحن قبل الـ checkout
        [HttpPost("calculate")]
        [AllowAnonymous]
        public async Task<IActionResult> Calculate([FromBody] CalculateShippingDto dto)
        {
            var result = await _shippingService.CalculateShippingAsync(dto);
            return Ok(result);
        }
    }
}