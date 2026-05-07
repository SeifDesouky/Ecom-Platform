using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        [HttpGet("entity/{entityName}/{entityId}")]
        public async Task<IActionResult> GetByEntity(string entityName, string entityId)
        {
            var result = await _auditLogService.GetByEntityAsync(entityName, entityId);
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetByTenant(Guid tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _auditLogService.GetByTenantAsync(tenantId, page, pageSize);
            return Ok(result);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _auditLogService.GetByUserAsync(userId, page, pageSize);
            return Ok(result);
        }
    }
}