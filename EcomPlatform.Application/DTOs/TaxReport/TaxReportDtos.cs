namespace EcomPlatform.Application.DTOs.TaxReports
{
    // ════════════════════════════════════════════════════════════════
    // FILTER
    // ════════════════════════════════════════════════════════════════

    public class TaxReportFilterDto
    {
        public Guid TenantId { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        /// <summary>تصفية بحالة الفاتورة — null = كل الحالات</summary>
        public string? Status { get; set; }   // "Paid" | "Unpaid" | null
    }

    // ════════════════════════════════════════════════════════════════
    // VAT SUMMARY (الملخص الرئيسي)
    // ════════════════════════════════════════════════════════════════

    public class VatSummaryDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string VatNumber { get; set; } = string.Empty;
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        // ── Totals ────────────────────────────────────────────────────────
        public decimal TotalSales { get; set; }   // إجمالي المبيعات شامل الضريبة
        public decimal TotalSalesExVat { get; set; }   // المبيعات بدون ضريبة
        public decimal TotalVatCollected { get; set; }   // VAT محصَّل من العملاء
        public decimal TotalDiscount { get; set; }   // إجمالي الخصومات
        public decimal TotalShipping { get; set; }   // إجمالي الشحن
        public decimal NetVatPayable { get; set; }   // VAT المستحق للدفع للهيئة

        // ── Counts ────────────────────────────────────────────────────────
        public int TotalInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public int UnpaidInvoices { get; set; }

        // ── VAT Rate ──────────────────────────────────────────────────────
        public decimal VatRate { get; set; }   // مثال: 0.15

        // ── Monthly Breakdown ─────────────────────────────────────────────
        public List<VatMonthlyBreakdownDto> MonthlyBreakdown { get; set; } = new();

        // ── Invoice Lines ─────────────────────────────────────────────────
        public List<VatInvoiceLineDto> Invoices { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>تفاصيل ضريبة شهر معين</summary>
    public class VatMonthlyBreakdownDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;   // "January 2026"
        public decimal SalesExVat { get; set; }
        public decimal VatCollected { get; set; }
        public decimal TotalWithVat { get; set; }
        public int InvoiceCount { get; set; }
    }

    /// <summary>سطر فاتورة واحدة في تقرير الضريبة</summary>
    public class VatInvoiceLineDto
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }   // قبل الضريبة
        public decimal Discount { get; set; }
        public decimal VatAmount { get; set; }   // قيمة الضريبة
        public decimal Total { get; set; }   // شامل الضريبة
        public decimal VatRate { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // EXPORT REQUEST
    // ════════════════════════════════════════════════════════════════

    public class ExportTaxReportDto
    {
        public Guid TenantId { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string Format { get; set; } = "csv";   // "csv" | "excel"
    }
}
