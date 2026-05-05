using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class TicketReply : BaseEntity
    {
        public string Message { get; set; } = string.Empty;
        public bool IsStaff { get; set; } = false;

        // Relations
        public Guid TicketId { get; set; }
        public Ticket? Ticket { get; set; }
        public Guid CreatedById { get; set; }
        public User? CreatedBy { get; set; }
    }
}