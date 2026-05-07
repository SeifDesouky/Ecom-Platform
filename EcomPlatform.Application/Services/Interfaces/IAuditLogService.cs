using EcomPlatform.Application.Common;
using EcomPlatform.Core.Enums;
using EcomPlatform.Application.DTOs.AuditLogs;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task LogAsync(string entityName, string entityId, AuditAction action,
            Guid userId, Guid? tenantId, string oldValue = "", string newValue = "",
            string ipAddress = "", string userAgent = "");
        Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByEntityAsync(string entityName, string entityId);
        Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByTenantAsync(Guid tenantId, int page = 1, int pageSize = 50);
        Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 50);
    }
}