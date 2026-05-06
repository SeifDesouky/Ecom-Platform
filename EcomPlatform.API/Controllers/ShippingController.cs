using EcomPlatform.Application.DTOs.Shipping;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ShippingController : ControllerBase
    {
        private readonly IShippingService _shippingService;

        public ShippingController(IShippingService shippingService)
        {
            _shippingService = shippingService;
        }

        [HttpGet("zones/tenant/{tenantId}")]
        public async Task<IActionResult> GetZonesByTenant(Guid tenantId)
        {
            var result = await _shippingService.GetZonesByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("zones/{id}")]
        public async Task<IActionResult> GetZoneById(Guid id)
        {
            var result = await _shippingService.GetZoneByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("zones")]
        public async Task<IActionResult> CreateZone([FromBody] CreateShippingZoneDto dto)
        {
            var result = await _shippingService.CreateZoneAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("zones/{id}")]
        public async Task<IActionResult> DeleteZone(Guid id)
        {
            var result = await _shippingService.DeleteZoneAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("zones/{id}/toggle-status")]
        public async Task<IActionResult> ToggleZoneStatus(Guid id)
        {
            var result = await _shippingService.ToggleZoneStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("methods")]
        public async Task<IActionResult> CreateMethod([FromBody] CreateShippingMethodDto dto)
        {
            var result = await _shippingService.CreateMethodAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("methods/{id}")]
        public async Task<IActionResult> DeleteMethod(Guid id)
        {
            var result = await _shippingService.DeleteMethodAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("methods/{id}/toggle-status")]
        public async Task<IActionResult> ToggleMethodStatus(Guid id)
        {
            var result = await _shippingService.ToggleMethodStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("calculate")]
        [AllowAnonymous]
        public async Task<IActionResult> Calculate([FromBody] CalculateShippingDto dto)
        {
            var result = await _shippingService.CalculateShippingAsync(dto);
            return Ok(result);
        }
    }
}