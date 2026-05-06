using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Notifications
{
    public class NotificationResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? ActionUrl { get; set; }
        public string? Icon { get; set; }
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationStatsDto
    {
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public List<NotificationResponseDto> Notifications { get; set; } = new();
    }
}