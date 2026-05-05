using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Tickets
{
    public class CreateTicketDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;
        public string Category { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public Guid CreatedById { get; set; }
    }
}