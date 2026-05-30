using EcomPlatform.Application.DTOs.ExportReports;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Helpers;
using System.Globalization;


namespace EcomPlatform.Infrastructure.Services
{
    public class ExportReportService : IExportReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ExportReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ════════════════════════════════════════════════════════════════
        // ORDERS
        // ════════════════════════════════════════════════════════════════

        public async Task<ExportResultDto> ExportOrdersAsync(OrdersExportFilterDto filter)
        {
            var (dateFrom, dateTo) = NormalizeDates(filter.DateFrom, filter.DateTo);

            var orders = await _unitOfWork.Orders.FindAsync(o =>
                o.TenantId == filter.TenantId &&
                o.CreatedAt >= dateFrom &&
                o.CreatedAt <= dateTo);

            // فلترة بالحالة
            if (!string.IsNullOrWhiteSpace(filter.Status) &&
                Enum.TryParse<OrderStatus>(filter.Status, true, out var orderStatus))
                orders = orders.Where(o => o.Status == orderStatus);

            if (!string.IsNullOrWhiteSpace(filter.PaymentStatus) &&
                Enum.TryParse<PaymentStatus>(filter.PaymentStatus, true, out var payStatus))
                orders = orders.Where(o => o.PaymentStatus == payStatus);

            var rows = orders
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderExportRow
                {
                    OrderNumber = o.OrderNumber,
                    Date = o.CreatedAt,
                    Status = o.Status.ToString(),
                    PaymentStatus = o.PaymentStatus.ToString(),
                    CustomerName = o.CustomerName,
                    CustomerEmail = o.CustomerEmail,
                    CustomerPhone = o.CustomerPhone,
                    ShippingCity = o.ShippingCity,
                    ShippingCountry = o.ShippingCountry,
                    SubTotal = o.SubTotal,
                    Discount = o.Discount,
                    ShippingCost = o.ShippingCost,
                    Tax = o.Tax,
                    Total = o.Total,
                    PaidAt = o.PaidAt,
                    DeliveredAt = o.DeliveredAt
                })
                .ToList();

            var headers = new[]
            {
                "Order #", "Date", "Status", "Payment Status",
                "Customer Name", "Email", "Phone",
                "City", "Country",
                "SubTotal", "Discount", "Shipping", "Tax", "Total",
                "Paid At", "Delivered At"
            };

            Func<OrderExportRow, string[]> mapper = r => new[]
            {
                r.OrderNumber,
                r.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                r.Status, r.PaymentStatus,
                r.CustomerName, r.CustomerEmail, r.CustomerPhone,
                r.ShippingCity, r.ShippingCountry,
                r.SubTotal.ToString("F2", CultureInfo.InvariantCulture),
                r.Discount.ToString("F2", CultureInfo.InvariantCulture),
                r.ShippingCost.ToString("F2", CultureInfo.InvariantCulture),
                r.Tax.ToString("F2", CultureInfo.InvariantCulture),
                r.Total.ToString("F2", CultureInfo.InvariantCulture),
                r.PaidAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                r.DeliveredAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""
            };

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(filter.TenantId);
            var subtitle = $"{tenant?.Name} | {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}";
            var fileBase = $"orders-{dateFrom:yyyyMMdd}-{dateTo:yyyyMMdd}";

            return BuildResult(filter.Format, rows, headers, mapper,
                title: "Orders Report", subtitle: subtitle, fileBase: fileBase);
        }

        // ════════════════════════════════════════════════════════════════
        // PRODUCTS
        // ════════════════════════════════════════════════════════════════

        public async Task<ExportResultDto> ExportProductsAsync(ProductsExportFilterDto filter)
        {
            var (dateFrom, dateTo) = NormalizeDates(filter.DateFrom, filter.DateTo);

            var products = await _unitOfWork.Products.FindAsync(p =>
                p.TenantId == filter.TenantId &&
                p.CreatedAt >= dateFrom &&
                p.CreatedAt <= dateTo);

            if (filter.LowStockOnly == true)
                products = products.Where(p => p.Stock <= p.LowStockAlert);

            if (!string.IsNullOrWhiteSpace(filter.CategoryId) &&
                Guid.TryParse(filter.CategoryId, out var catId))
                products = products.Where(p => p.CategoryId == catId);

            // جيب أسماء الكاتيجوريز دفعة واحدة
            var catIds = products.Select(p => p.CategoryId).Distinct().ToHashSet();
            var cats = (await _unitOfWork.Categories.FindAsync(c => catIds.Contains(c.Id)))
                            .ToDictionary(c => c.Id, c => c.Name);

            var rows = products
                .OrderBy(p => p.Name)
                .Select(p => new ProductExportRow
                {
                    Name = p.Name,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    Category = cats.GetValueOrDefault(p.CategoryId, string.Empty),
                    Price = p.Price,
                    ComparePrice = p.ComparePrice,
                    CostPrice = p.CostPrice,
                    Stock = p.Stock,
                    LowStockAlert = p.LowStockAlert,
                    Status = p.Status.ToString(),
                    IsActive = p.IsActive,
                    IsFeatured = p.IsFeatured,
                    Weight = p.Weight,
                    CreatedAt = p.CreatedAt
                })
                .ToList();

            var headers = new[]
            {
                "Name", "SKU", "Barcode", "Category",
                "Price", "Compare Price", "Cost Price",
                "Stock", "Low Stock Alert", "Status",
                "Active", "Featured", "Weight (kg)", "Created At"
            };

            Func<ProductExportRow, string[]> mapper = r => new[]
            {
                r.Name, r.SKU, r.Barcode, r.Category,
                r.Price.ToString("F2", CultureInfo.InvariantCulture),
                r.ComparePrice?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                r.CostPrice?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                r.Stock.ToString(), r.LowStockAlert.ToString(),
                r.Status,
                r.IsActive ? "Yes" : "No",
                r.IsFeatured ? "Yes" : "No",
                r.Weight.ToString("F3", CultureInfo.InvariantCulture),
                r.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(filter.TenantId);
            var subtitle = $"{tenant?.Name} | {rows.Count} products";
            var fileBase = $"products-{DateTime.UtcNow:yyyyMMdd}";

            return BuildResult(filter.Format, rows, headers, mapper,
                title: "Products Report", subtitle: subtitle, fileBase: fileBase);
        }

        // ════════════════════════════════════════════════════════════════
        // CUSTOMERS
        // ════════════════════════════════════════════════════════════════

        public async Task<ExportResultDto> ExportCustomersAsync(CustomersExportFilterDto filter)
        {
            var (dateFrom, dateTo) = NormalizeDates(filter.DateFrom, filter.DateTo);

            var customers = await _unitOfWork.Customers.FindAsync(c =>
                c.TenantId == filter.TenantId &&
                c.CreatedAt >= dateFrom &&
                c.CreatedAt <= dateTo);

            if (filter.ActiveOnly == true)
                customers = customers.Where(c => c.IsActive);

            var rows = customers
                .OrderByDescending(c => c.TotalSpent)
                .Select(c => new CustomerExportRow
                {
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    Email = c.Email,
                    Phone = c.Phone,
                    IsActive = c.IsActive,
                    TotalSpent = c.TotalSpent,
                    TotalOrders = c.TotalOrders,
                    BirthDate = c.BirthDate,
                    CreatedAt = c.CreatedAt
                })
                .ToList();

            var headers = new[]
            {
                "First Name", "Last Name", "Email", "Phone",
                "Active", "Total Spent", "Total Orders",
                "Birth Date", "Joined At"
            };

            Func<CustomerExportRow, string[]> mapper = r => new[]
            {
                r.FirstName, r.LastName, r.Email, r.Phone,
                r.IsActive ? "Yes" : "No",
                r.TotalSpent.ToString("F2", CultureInfo.InvariantCulture),
                r.TotalOrders.ToString(),
                r.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                r.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(filter.TenantId);
            var subtitle = $"{tenant?.Name} | {rows.Count} customers";
            var fileBase = $"customers-{DateTime.UtcNow:yyyyMMdd}";

            return BuildResult(filter.Format, rows, headers, mapper,
                title: "Customers Report", subtitle: subtitle, fileBase: fileBase);
        }

        // ════════════════════════════════════════════════════════════════
        // INVENTORY
        // ════════════════════════════════════════════════════════════════

        public async Task<ExportResultDto> ExportInventoryAsync(ExportFilterDto filter)
        {
            var products = await _unitOfWork.Products.FindAsync(p =>
                p.TenantId == filter.TenantId);

            var catIds = products.Select(p => p.CategoryId).Distinct().ToHashSet();
            var cats = (await _unitOfWork.Categories.FindAsync(c => catIds.Contains(c.Id)))
                         .ToDictionary(c => c.Id, c => c.Name);

            var rows = products
                .OrderBy(p => p.Name)
                .Select(p => new InventoryExportRow
                {
                    ProductName = p.Name,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    Category = cats.GetValueOrDefault(p.CategoryId, string.Empty),
                    CurrentStock = p.Stock,
                    LowStockAlert = p.LowStockAlert,
                    IsLowStock = p.Stock <= p.LowStockAlert && p.Stock > 0,
                    IsOutOfStock = p.Stock == 0,
                    Price = p.Price,
                    CostPrice = p.CostPrice,
                    StockValue = p.Stock * (p.CostPrice ?? p.Price),
                    Status = p.Status.ToString()
                })
                .ToList();

            var headers = new[]
            {
                "Product Name", "SKU", "Barcode", "Category",
                "Current Stock", "Low Stock Alert",
                "Low Stock?", "Out of Stock?",
                "Price", "Cost Price", "Stock Value", "Status"
            };

            Func<InventoryExportRow, string[]> mapper = r => new[]
            {
                r.ProductName, r.SKU, r.Barcode, r.Category,
                r.CurrentStock.ToString(),
                r.LowStockAlert.ToString(),
                r.IsLowStock  ? "⚠️ Yes" : "No",
                r.IsOutOfStock ? "❌ Yes" : "No",
                r.Price.ToString("F2", CultureInfo.InvariantCulture),
                r.CostPrice?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                r.StockValue.ToString("F2", CultureInfo.InvariantCulture),
                r.Status
            };

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(filter.TenantId);
            var subtitle = $"{tenant?.Name} | {rows.Count} products";
            var fileBase = $"inventory-{DateTime.UtcNow:yyyyMMdd}";

            return BuildResult(filter.Format, rows, headers, mapper,
                title: "Inventory Report", subtitle: subtitle, fileBase: fileBase);
        }

        // ════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════

        private static (DateTime from, DateTime to) NormalizeDates(
            DateTime? from, DateTime? to)
        {
            var dateFrom = (from ?? DateTime.UtcNow.AddYears(-1)).Date;
            var dateTo = (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
            return (dateFrom, dateTo);
        }

        private static ExportResultDto BuildResult<T>(
            string format,
            List<T> rows,
            string[] headers,
            Func<T, string[]> mapper,
            string title,
            string subtitle,
            string fileBase)
        {
            byte[] bytes;
            string contentType;
            string ext;

            switch (format.ToLower())
            {
                case "excel":
                case "xlsx":
                case "xls":
                    bytes = CsvBuilder.ToExcel(rows, headers, mapper, $"{title} — {subtitle}");
                    contentType = "application/vnd.ms-excel";
                    ext = "xls";
                    break;

                case "pdf":
                    bytes = CsvBuilder.ToPdfHtml(rows, headers, mapper, title, subtitle);
                    contentType = "text/html; charset=utf-8";
                    ext = "html";    // المتصفح يفتحه ويطبعه كـ PDF
                    break;

                default:   // csv
                    bytes = CsvBuilder.ToCsv(rows, headers, mapper);
                    contentType = "text/csv; charset=utf-8";
                    ext = "csv";
                    break;
            }

            return new ExportResultDto
            {
                Bytes = bytes,
                FileName = $"{fileBase}.{ext}",
                ContentType = contentType,
                RowCount = rows.Count
            };
        }
    }
}
