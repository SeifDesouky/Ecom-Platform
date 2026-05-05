namespace EcomPlatform.Application.DTOs.Tickets
{
    public class CreateTicketReplyDto
    {
        public string Message { get; set; } = string.Empty;
        public bool IsStaff { get; set; } = false;
        public Guid TicketId { get; set; }
        public Guid CreatedById { get; set; }
    }
}