using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Tenants;
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
    public class TenantsController : BaseController
    {
        private readonly ITenantService _tenantService;

        public TenantsController(
            ITenantService tenantService,
            IAuditLogService auditLogService)
            : base(auditLogService)
        {
            _tenantService = tenantService;
        }

        // ============================
        // Get All Tenants
        // SuperAdmin Only
        // ============================
        [HttpGet]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _tenantService.GetAllAsync();
            return Ok(result);
        }

        // ============================
        // Get Tenant By Id
        // TenantAdmin Or Above
        // ============================
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _tenantService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        // ============================
        // Create Tenant
        // SuperAdmin Only
        // ============================
        [HttpPost]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Create([FromBody] CreateTenantDto dto)
        {
            var result = await _tenantService.CreateAsync(dto);

            if (!result.Success)
                return BadRequest(result);

            await LogAudit(
                entityName: "Tenant",
                entityId: result.Data!.Id.ToString(),
                action: AuditAction.Create,
                tenantId: result.Data.Id,
                oldValue: null,
                newValue: $"Tenant '{result.Data.Name}' created"
            );

            return Ok(result);
        }

        // ============================
        // Update Tenant
        // TenantAdmin Or Above
        // ============================
        [HttpPut("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateTenantDto dto)
        {
            var existing = await _tenantService.GetByIdAsync(id);

            if (!existing.Success)
                return NotFound(existing);

            var result = await _tenantService.UpdateAsync(id, dto);

            if (!result.Success)
                return BadRequest(result);

            await LogAudit(
                entityName: "Tenant",
                entityId: id.ToString(),
                action: AuditAction.Update,
                tenantId: id,
                oldValue: existing.Data?.Name ?? "",
                newValue: result.Data?.Name ?? ""
            );

            return Ok(result);
        }

        // ============================
        // Delete Tenant
        // SuperAdmin Only
        // ============================
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _tenantService.GetByIdAsync(id);

            if (!existing.Success)
                return NotFound(existing);

            var result = await _tenantService.DeleteAsync(id);

            if (!result.Success)
                return NotFound(result);

            await LogAudit(
                entityName: "Tenant",
                entityId: id.ToString(),
                action: AuditAction.Delete,
                tenantId: id,
                oldValue: $"Tenant '{existing.Data?.Name}' deleted",
                newValue: null
            );

            return Ok(result);
        }

        // ============================
        // Toggle Tenant Status
        // SuperAdmin Only
        // ============================
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var existing = await _tenantService.GetByIdAsync(id);

            if (!existing.Success)
                return NotFound(existing);

            var result = await _tenantService.ToggleStatusAsync(id);

            if (!result.Success)
                return NotFound(result);

            await LogAudit(
                entityName: "Tenant",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                tenantId: id,
                oldValue: existing.Data?.IsActive.ToString() ?? "",
                newValue: (!existing.Data?.IsActive ?? false).ToString()
            );

            return Ok(result);
        }
    }
}