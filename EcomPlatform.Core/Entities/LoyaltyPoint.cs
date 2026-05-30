using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// Ledger entry لنقاط الولاء.
    /// كل حركة (ربح / صرف / تعديل) تُسجَّل كسطر مستقل.
    /// الرصيد الحالي = SUM(Points) لكل العمليات.
    /// </summary>
    public class LoyaltyPoint : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public LoyaltyTransactionType Type { get; set; }

        /// <summary>موجب للربح والبونص، سالب للصرف والانتهاء</summary>
        public int Points { get; set; }

        /// <summary>رصيد العميل بعد هذه المعاملة مباشرةً</summary>
        public int BalanceAfter { get; set; }

        /// <summary>مرجع: رقم الأوردر، رقم الإرجاع، إلخ.</summary>
        public string Reference { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        /// <summary>تاريخ انتهاء صلاحية النقاط المكتسبة في هذه المعاملة</summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
