using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.EmailMarketing
{
    // ════════════════════════════════════════════════════════════════
    // MAILING LIST
    // ════════════════════════════════════════════════════════════════

    public class CreateMailingListDto
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? WelcomeEmailSubject { get; set; }
        public string? WelcomeEmailBody { get; set; }
    }

    public class UpdateMailingListDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? WelcomeEmailSubject { get; set; }
        public string? WelcomeEmailBody { get; set; }
    }

    public class MailingListResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SubscriberCount { get; set; }
        public int ActiveCount { get; set; }
        public string? WelcomeEmailSubject { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // SUBSCRIBERS
    // ════════════════════════════════════════════════════════════════

    public class AddSubscriberDto
    {
        public Guid TenantId { get; set; }
        public Guid MailingListId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        public string Source { get; set; } = "Manual";
    }

    /// <summary>استيراد مشتركين بالجملة من CSV أو قائمة</summary>
    public class ImportSubscribersDto
    {
        public Guid TenantId { get; set; }
        public Guid MailingListId { get; set; }
        public string Source { get; set; } = "Import";
        public List<AddSubscriberDto> Subscribers { get; set; } = new();
    }

    public class ImportResultDto
    {
        public int Added { get; set; }
        public int Skipped { get; set; }   // موجود بالفعل
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class SubscriberResponseDto
    {
        public Guid Id { get; set; }
        public Guid MailingListId { get; set; }
        public string MailingListName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public SubscriberStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        public DateTime? UnsubscribedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // CAMPAIGNS
    // ════════════════════════════════════════════════════════════════

    public class CreateCampaignDto
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
        public string TextBody { get; set; } = string.Empty;

        /// <summary>القوائم البريدية المستهدفة</summary>
        public List<Guid> MailingListIds { get; set; } = new();

        /// <summary>وقت الجدولة — null = إرسال فوري عند استدعاء Send</summary>
        public DateTime? ScheduledAt { get; set; }
    }

    public class UpdateCampaignDto
    {
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
        public string TextBody { get; set; } = string.Empty;
        public List<Guid> MailingListIds { get; set; } = new();
        public DateTime? ScheduledAt { get; set; }
    }

    public class CampaignResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
        public CampaignStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? SentAt { get; set; }
        public List<string> MailingListNames { get; set; } = new();

        // Stats
        public int TotalRecipients { get; set; }
        public int SentCount { get; set; }
        public int OpenedCount { get; set; }
        public int ClickedCount { get; set; }
        public int BouncedCount { get; set; }
        public int UnsubscribedCount { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // TRACKING (Webhook / Pixel hits)
    // ════════════════════════════════════════════════════════════════

    public class TrackOpenDto
    {
        public string TrackingToken { get; set; } = string.Empty;
    }

    public class TrackClickDto
    {
        public string TrackingToken { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}