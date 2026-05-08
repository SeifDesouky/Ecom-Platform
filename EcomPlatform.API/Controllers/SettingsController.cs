using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Settings;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        // Staff وفوق — يقرأ settings الـ tenant (محتاجها في لوحة التحكم)
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _settingService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        // Staff وفوق — يجيب setting معينة بالـ key
        [HttpGet("{key}/tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByKey(string key, Guid tenantId)
        {
            var result = await _settingService.GetByKeyAsync(key, tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إنشاء setting جديدة
        [HttpPost]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateSettingDto dto)
        {
            var result = await _settingService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تعديل setting
        [HttpPut("{key}/tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Update(string key, Guid tenantId, [FromBody] UpdateSettingDto dto)
        {
            var result = await _settingService.UpdateAsync(key, dto, tenantId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تحديث مجموعة settings دفعة واحدة
        [HttpPut("bulk-update")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateSettingDto dto)
        {
            var result = await _settingService.BulkUpdateAsync(dto);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف setting
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _settingService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — initialize default settings لـ tenant جديد
        [HttpPost("initialize/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Initialize(Guid tenantId)
        {
            var result = await _settingService.InitializeDefaultSettingsAsync(tenantId);
            return Ok(result);
        }
    }
}