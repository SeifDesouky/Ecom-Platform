using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        // TenantAdmin وفوق — يشوف audit logs لـ entity معينة
        [HttpGet("entity/{entityName}/{entityId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetByEntity(string entityName, string entityId)
        {
            var result = await _auditLogService.GetByEntityAsync(entityName, entityId);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف audit logs الـ tenant مع pagination
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetByTenant(
            Guid tenantId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var result = await _auditLogService.GetByTenantAsync(tenantId, page, pageSize);
            return Ok(result);
        }

        // TenantAdmin وفوق — audit logs لـ user معين
        [HttpGet("user/{userId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetByUser(
            Guid userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var result = await _auditLogService.GetByUserAsync(userId, page, pageSize);
            return Ok(result);
        }
    }
}