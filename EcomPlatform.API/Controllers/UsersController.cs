using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Users;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // SuperAdmin فقط — كل يوزرز المنصة مع search + paging
        [HttpGet("all")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20)
        {
            var result = await _userService.GetAllAsync(search, page, limit);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف users الـ tenant بتاعه
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _userService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف user معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _userService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إنشاء user جديد في الـ tenant
        [HttpPost]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            var result = await _userService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تعديل بيانات user
        [HttpPut("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
        {
            var result = await _userService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف user
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _userService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تفعيل/تعطيل user
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _userService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تغيير role الـ user
        [HttpPatch("{id}/change-role")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ChangeRole(Guid id, [FromBody] UserRole role)
        {
            var result = await _userService.ChangeRoleAsync(id, role);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — reset password لـ user
        [HttpPatch("{id}/reset-password")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ResetPassword(Guid id, [FromBody] string newPassword)
        {
            var result = await _userService.ResetPasswordAsync(id, newPassword);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}