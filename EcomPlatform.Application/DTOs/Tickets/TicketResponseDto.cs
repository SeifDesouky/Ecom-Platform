using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Tickets
{
    public class TicketResponseDto
    {
        public Guid Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public TicketPriority Priority { get; set; }
        public string Category { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public Guid CreatedById { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<TicketReplyResponseDto> Replies { get; set; } = new();
    }

    public class TicketReplyResponseDto
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsStaff { get; set; }
        public Guid CreatedById { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}