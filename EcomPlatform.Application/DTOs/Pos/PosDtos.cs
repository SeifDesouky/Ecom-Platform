using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Pos
{
    // ════════════════════════════════════════════════════════════════
    // SESSION DTOs
    // ════════════════════════════════════════════════════════════════

    public class OpenPosSessionDto
    {
        public Guid TenantId { get; set; }
        public string TerminalName { get; set; } = "POS-1";
        /// <summary>النقدي الأول في الدرج</summary>
        public decimal OpeningCash { get; set; }
    }

    public class ClosePosSessionDto
    {
        /// <summary>النقدي الفعلي في الدرج وقت الإغلاق</summary>
        public decimal ClosingCash { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class PosSessionResponseDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid CashierId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string TerminalName { get; set; } = string.Empty;
        public PosSessionStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public decimal OpeningCash { get; set; }
        public decimal? ClosingCash { get; set; }
        public decimal? ExpectedCash { get; set; }
        public decimal? CashDifference { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalCardSales { get; set; }
        public decimal TotalRefunds { get; set; }
        public int OrdersCount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    // ════════════════════════════════════════════════════════════════
    // ORDER / SALE DTOs
    // ════════════════════════════════════════════════════════════════

    public class CreatePosOrderDto
    {
        public Guid TenantId { get; set; }
        public Guid PosSessionId { get; set; }
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public PosPaymentMethod PaymentMethod { get; set; } = PosPaymentMethod.Cash;
        /// <summary>المبلغ المُسلَّم نقداً من العميل</summary>
        public decimal CashTendered { get; set; }
        /// <summary>المبلغ المدفوع بالكارت (في Mixed)</summary>
        public decimal CardPaid { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? CouponCode { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<CreatePosOrderItemDto> Items { get; set; } = new();
    }

    public class CreatePosOrderItemDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        /// <summary>لو السعر اتغير على السطر (Override)</summary>
        public decimal? OverridePrice { get; set; }
        public decimal LineDiscount { get; set; }
    }

    public class VoidPosOrderDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class PosOrderResponseDto
    {
        public Guid Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public Guid PosSessionId { get; set; }
        public string TerminalName { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public PosOrderStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public decimal CashPaid { get; set; }
        public decimal CardPaid { get; set; }
        public decimal Change { get; set; }
        public PosPaymentMethod PaymentMethod { get; set; }
        public string PaymentMethodLabel { get; set; } = string.Empty;
        public string? CouponCode { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PosOrderItemResponseDto> Items { get; set; } = new();

        // ── بيانات إضافية للطباعة الحرارية ──────────────────────────────
        public string TenantName { get; set; } = string.Empty;
        public string TenantLogo { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public string TenantAddress { get; set; } = string.Empty;
        public string TenantVatNumber { get; set; } = string.Empty;
    }

    public class PosOrderItemResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscount { get; set; }
        public decimal TotalPrice { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // QUICK PRODUCT SEARCH (للبحث بالباركود أو الاسم عند الكاشير)
    // ════════════════════════════════════════════════════════════════

    public class PosProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool TrackInventory { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // SESSION SUMMARY (تقرير الجلسة عند الإغلاق)
    // ════════════════════════════════════════════════════════════════

    public class PosSessionSummaryDto
    {
        public Guid SessionId { get; set; }
        public string TerminalName { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public DateTime OpenedAt { get; set; }
        public DateTime ClosedAt { get; set; }
        public decimal OpeningCash { get; set; }
        public decimal ClosingCash { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal CashDifference { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalCardSales { get; set; }
        public decimal TotalRefunds { get; set; }
        public int TotalOrders { get; set; }
        public int VoidedOrders { get; set; }
        /// <summary>توزيع المبيعات على الكاتيجوريز</summary>
        public List<PosCategorySalesDto> SalesByCategory { get; set; } = new();
        /// <summary>المنتجات الأكثر مبيعاً في الجلسة</summary>
        public List<PosTopProductDto> TopProducts { get; set; } = new();
    }

    public class PosCategorySalesDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ItemsSold { get; set; }
        public decimal TotalSales { get; set; }
    }

    public class PosTopProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal TotalSales { get; set; }
    }
}
