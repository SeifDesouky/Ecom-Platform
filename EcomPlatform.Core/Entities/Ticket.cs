using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class Ticket : BaseEntity, ITenantEntity
    {
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TicketStatus Status { get; set; } = TicketStatus.Open;
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;
        public string Category { get; set; } = string.Empty;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public Guid CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        // Navigation
        public ICollection<TicketReply> Replies { get; set; } = new List<TicketReply>();
    }
}