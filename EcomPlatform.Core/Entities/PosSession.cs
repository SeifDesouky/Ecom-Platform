using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// Session الكاشير — من فتح الدرج لحد إقفاله.
    /// كل عملية بيع POS تنتمي لـ Session مفتوحة.
    /// </summary>
    public class PosSession : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        /// <summary>الكاشير (User بـ Role = TenantStaff أو TenantAdmin)</summary>
        public Guid CashierId { get; set; }
        public User? Cashier { get; set; }

        /// <summary>اسم أو رقم نقطة البيع (لو في أكتر من ترمينال)</summary>
        public string TerminalName { get; set; } = "POS-1";

        public PosSessionStatus Status { get; set; } = PosSessionStatus.Open;

        /// <summary>رصيد الدرج عند الفتح (Opening Float)</summary>
        public decimal OpeningCash { get; set; }

        /// <summary>رصيد الدرج الفعلي عند الإغلاق (Closing Float)</summary>
        public decimal? ClosingCash { get; set; }

        /// <summary>النقدي المتوقع وقت الإغلاق (حسابياً من المبيعات)</summary>
        public decimal? ExpectedCash { get; set; }

        /// <summary>الفرق بين الفعلي والمتوقع (+زيادة / -عجز)</summary>
        public decimal? CashDifference { get; set; }

        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }

        public string Notes { get; set; } = string.Empty;

        // ── Totals (يُحسَب عند الإغلاق) ──────────────────────────────────────
        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalCardSales { get; set; }
        public decimal TotalRefunds { get; set; }
        public int OrdersCount { get; set; }

        // Navigation
        public ICollection<PosOrder> Orders { get; set; } = new List<PosOrder>();
    }
}
