namespace EcomPlatform.Application.DTOs.ExportReports
{
    // ════════════════════════════════════════════════════════════════
    // FILTER — مشترك لكل التقارير
    // ════════════════════════════════════════════════════════════════

    public class ExportFilterDto
    {
        public Guid TenantId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        /// <summary>csv | excel | pdf</summary>
        public string Format { get; set; } = "csv";
    }

    public class OrdersExportFilterDto : ExportFilterDto
    {
        /// <summary>null = كل الحالات</summary>
        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
    }

    public class ProductsExportFilterDto : ExportFilterDto
    {
        public string? CategoryId { get; set; }
        public bool? LowStockOnly { get; set; }
    }

    public class CustomersExportFilterDto : ExportFilterDto
    {
        public bool? ActiveOnly { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    // ROW DTOs (البيانات اللي بتتحول لصفوف في التقرير)
    // ════════════════════════════════════════════════════════════════

    public class OrderExportRow
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingCountry { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    public class ProductExportRow
    {
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public decimal? CostPrice { get; set; }
        public int Stock { get; set; }
        public int LowStockAlert { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public decimal Weight { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerExportRow
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal TotalSpent { get; set; }
        public int TotalOrders { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InventoryExportRow
    {
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int LowStockAlert { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsOutOfStock { get; set; }
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal StockValue { get; set; }  // Stock × CostPrice
        public string Status { get; set; } = string.Empty;
    }

    // ════════════════════════════════════════════════════════════════
    // EXPORT RESULT
    // ════════════════════════════════════════════════════════════════

    public class ExportResultDto
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/csv";
        public int RowCount { get; set; }
    }
}
