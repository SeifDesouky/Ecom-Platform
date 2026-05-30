using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// حملة تسويقية بريدية — ترتبط بقائمة أو أكتر وتُرسَل مرة واحدة
    /// </summary>
    public class Campaign : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;        // اسم داخلي
        public string Subject { get; set; } = string.Empty;     // سطر الموضوع
        public string PreviewText { get; set; } = string.Empty; // النص الظاهر قبل الفتح
        public string FromName { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;

        /// <summary>HTML الكامل للإيميل</summary>
        public string HtmlBody { get; set; } = string.Empty;

        /// <summary>نسخة نص عادي (Plain Text)</summary>
        public string TextBody { get; set; } = string.Empty;

        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

        /// <summary>وقت الجدولة — null = فوري عند الإرسال</summary>
        public DateTime? ScheduledAt { get; set; }

        public DateTime? SentAt { get; set; }

        // ── Stats (يُحدَّث مع كل Event) ──────────────────────────────────────
        public int TotalRecipients { get; set; }
        public int SentCount { get; set; }
        public int DeliveredCount { get; set; }
        public int OpenedCount { get; set; }
        public int ClickedCount { get; set; }
        public int BouncedCount { get; set; }
        public int UnsubscribedCount { get; set; }

        // Navigation
        public ICollection<CampaignMailingList> MailingLists { get; set; } = new List<CampaignMailingList>();
        public ICollection<CampaignRecipient> Recipients { get; set; } = new List<CampaignRecipient>();
    }

    /// <summary>ربط Many-to-Many بين Campaign و MailingList</summary>
    public class CampaignMailingList : BaseEntity  // ✅ بيرث من BaseEntity عشان يشتغل مع IRepository
    {
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        public Guid MailingListId { get; set; }
        public MailingList? MailingList { get; set; }
    }

    /// <summary>
    /// سجل الإرسال لكل مستلم — يُستخدم لتتبع الفتح والنقر
    /// </summary>
    public class CampaignRecipient : BaseEntity
    {
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public CampaignRecipientStatus Status { get; set; } = CampaignRecipientStatus.Pending;

        /// <summary>Token فريد لتتبع الفتح والنقر</summary>
        public string TrackingToken { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime? SentAt { get; set; }
        public DateTime? OpenedAt { get; set; }
        public DateTime? ClickedAt { get; set; }
        public DateTime? BouncedAt { get; set; }
        public string? FailReason { get; set; }
    }
}