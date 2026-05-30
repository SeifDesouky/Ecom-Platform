using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Reviews
{
    // ════════════════════════════════════════════════════════════════
    // INPUT DTOs
    // ════════════════════════════════════════════════════════════════

    /// <summary>يُرسَل من العميل (مسجَّل أو زائر)</summary>
    public class CreateReviewDto
    {
        public Guid TenantId { get; set; }
        public Guid ProductId { get; set; }

        /// <summary>لو المُقيِّم عميل مسجَّل</summary>
        public Guid? CustomerId { get; set; }

        /// <summary>مطلوب لو CustomerId فارغ</summary>
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;

        /// <summary>1 إلى 5</summary>
        public int Rating { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>رد صاحب المتجر على تقييم</summary>
    public class OwnerReplyDto
    {
        public string Reply { get; set; } = string.Empty;
    }

    /// <summary>تغيير حالة التقييم (Approve / Reject / Spam)</summary>
    public class UpdateReviewStatusDto
    {
        public ReviewStatus Status { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // OUTPUT DTOs
    // ════════════════════════════════════════════════════════════════

    public class ReviewResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public ReviewStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public bool IsVerifiedPurchase { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? OwnerRepliedAt { get; set; }
        public int HelpfulCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>ملخص التقييمات على المنتج — للعرض في صفحة المنتج</summary>
    public class ProductRatingSummaryDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        /// <summary>توزيع التقييمات: مفتاح = عدد النجوم (1-5) ، قيمة = عدد المراجعات</summary>
        public Dictionary<int, int> RatingBreakdown { get; set; } = new();
        /// <summary>أحدث المراجعات المعتمدة للعرض المباشر</summary>
        public List<ReviewResponseDto> RecentReviews { get; set; } = new();
    }
}