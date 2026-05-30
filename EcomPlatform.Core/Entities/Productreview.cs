using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// تقييم ومراجعة منتج.
    /// العميل المسجَّل يُربَط بـ CustomerId.
    /// زائر بدون حساب يكتب اسمه وإيميله يدوياً.
    /// </summary>
    public class ProductReview : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        /// <summary>العميل المسجَّل — nullable لو كان زائر</summary>
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        /// <summary>اسم المُقيِّم (يُملأ تلقائياً من Customer لو كان مسجَّلاً)</summary>
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;

        /// <summary>التقييم من 1 إلى 5 نجوم</summary>
        public int Rating { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

        /// <summary>مشترى فعلاً (Verified Purchase) — يُفعَّل تلقائياً لو العميل اشترى المنتج</summary>
        public bool IsVerifiedPurchase { get; set; } = false;

        /// <summary>رد صاحب المتجر على التقييم</summary>
        public string? OwnerReply { get; set; }
        public DateTime? OwnerRepliedAt { get; set; }

        /// <summary>عدد الـ Helpful votes من بقية العملاء</summary>
        public int HelpfulCount { get; set; } = 0;
    }
}