using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Loyalty
{
    // ════════════════════════════════════════════════════════════════
    // INPUT DTOs
    // ════════════════════════════════════════════════════════════════

    /// <summary>إضافة نقاط يدوياً (Bonus أو تعديل من الأدمن)</summary>
    public class AdjustLoyaltyDto
    {
        public Guid TenantId { get; set; }
        public Guid CustomerId { get; set; }

        /// <summary>موجب للإضافة، سالب للخصم اليدوي</summary>
        public int Points { get; set; }

        public LoyaltyTransactionType Type { get; set; } = LoyaltyTransactionType.Adjusted;
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>صرف نقاط كخصم على أوردر</summary>
    public class RedeemLoyaltyDto
    {
        public Guid TenantId { get; set; }
        public Guid CustomerId { get; set; }

        /// <summary>عدد النقاط المراد صرفها</summary>
        public int Points { get; set; }

        /// <summary>رقم الأوردر مرجعاً</summary>
        public string OrderReference { get; set; } = string.Empty;
    }

    // ════════════════════════════════════════════════════════════════
    // OUTPUT DTOs
    // ════════════════════════════════════════════════════════════════

    public class LoyaltyTransactionDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public LoyaltyTransactionType Type { get; set; }
        public string TypeLabel { get; set; } = string.Empty;
        public int Points { get; set; }
        public int BalanceAfter { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LoyaltyBalanceDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int Balance { get; set; }

        /// <summary>قيمة النقاط بالعملة بناءً على إعداد loyalty_points_value</summary>
        public decimal MonetaryValue { get; set; }
        public string Currency { get; set; } = "SAR";

        /// <summary>نقاط ستنتهي قريباً (خلال 30 يوم)</summary>
        public int ExpiringPoints { get; set; }
        public DateTime? NearestExpiry { get; set; }
    }

    /// <summary>نتيجة عملية الصرف — يُستخدم في الأوردر لمعرفة قيمة الخصم</summary>
    public class RedeemResultDto
    {
        public int PointsRedeemed { get; set; }
        public decimal DiscountAmount { get; set; }
        public int NewBalance { get; set; }
    }
}
