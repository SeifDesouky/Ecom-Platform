using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    public abstract class BaseController : ControllerBase
    {
        protected readonly IAuditLogService _auditLogService;

        protected BaseController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        protected Guid? GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        protected Guid? GetTenantId()
        {
            var claim = User.FindFirstValue("tenantId");
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        protected string GetIpAddress() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

        protected async Task LogAudit(
            string entityName,
            string entityId,
            AuditAction action,
            Guid? tenantId,
            string oldValue = "",
            string newValue = "")
        {
            var userId = GetUserId();
            if (userId == null) return;

            await _auditLogService.LogAsync(
                entityName, entityId, action,
                userId.Value, tenantId,
                oldValue: oldValue,
                newValue: newValue,
                ipAddress: GetIpAddress());
        }
    }
}