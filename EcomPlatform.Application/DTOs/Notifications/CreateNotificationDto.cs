using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Notifications
{
    public class CreateNotificationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public string? ActionUrl { get; set; }
        public string? Icon { get; set; }
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }
    }
}