using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// قائمة بريدية — مجموعة مشتركين تحت اسم واحد
    /// مثال: "عملاء VIP" — "مهتمون بالعروض" — "Newsletter"
    /// </summary>
    public class MailingList : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>رسالة الترحيب التي تُرسَل تلقائياً عند الاشتراك</summary>
        public string? WelcomeEmailSubject { get; set; }
        public string? WelcomeEmailBody { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<MailingListSubscriber> Subscribers { get; set; } = new List<MailingListSubscriber>();
        public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    }

    /// <summary>
    /// مشترك في قائمة بريدية واحدة.
    /// نفس الإيميل ممكن يكون في أكتر من قائمة بصفوف منفصلة.
    /// </summary>
    public class MailingListSubscriber : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid MailingListId { get; set; }
        public MailingList? MailingList { get; set; }

        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        /// <summary>لو كان عميل مسجَّل — ربط اختياري</summary>
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public SubscriberStatus Status { get; set; } = SubscriberStatus.Active;

        /// <summary>مصدر الاشتراك: Manual، Import، Checkout، Signup</summary>
        public string Source { get; set; } = "Manual";

        public DateTime? UnsubscribedAt { get; set; }

        /// <summary>Token سري للـ Unsubscribe Link</summary>
        public string UnsubscribeToken { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>بيانات إضافية مخصصة (JSON)</summary>
        public string? CustomFields { get; set; }
    }
}