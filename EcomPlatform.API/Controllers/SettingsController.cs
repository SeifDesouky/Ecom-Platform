using EcomPlatform.Application.DTOs.Settings;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _settingService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{key}/tenant/{tenantId}")]
        public async Task<IActionResult> GetByKey(string key, Guid tenantId)
        {
            var result = await _settingService.GetByKeyAsync(key, tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSettingDto dto)
        {
            var result = await _settingService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{key}/tenant/{tenantId}")]
        public async Task<IActionResult> Update(string key, Guid tenantId, [FromBody] UpdateSettingDto dto)
        {
            var result = await _settingService.UpdateAsync(key, dto, tenantId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("bulk-update")]
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateSettingDto dto)
        {
            var result = await _settingService.BulkUpdateAsync(dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _settingService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("initialize/{tenantId}")]
        public async Task<IActionResult> Initialize(Guid tenantId)
        {
            var result = await _settingService.InitializeDefaultSettingsAsync(tenantId);
            return Ok(result);
        }
    }
}