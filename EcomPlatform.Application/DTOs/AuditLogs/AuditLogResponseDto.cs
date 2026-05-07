using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.AuditLogs
{
    public class AuditLogResponseDto
    {
        public Guid Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public AuditAction Action { get; set; }
        public string ActionName => Action.ToString();
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}