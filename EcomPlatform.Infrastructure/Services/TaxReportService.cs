using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.TaxReports;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using System.Globalization;
using System.Text;

namespace EcomPlatform.Infrastructure.Services
{
    public class TaxReportService : ITaxReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TaxReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ════════════════════════════════════════════════════════════════
        // VAT SUMMARY
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<VatSummaryDto>> GetVatSummaryAsync(TaxReportFilterDto filter)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(filter.TenantId);
            if (tenant == null)
                return ApiResponse<VatSummaryDto>.Fail("Tenant not found.");

            var dateFrom = filter.DateFrom.Date;
            var dateTo = filter.DateTo.Date.AddDays(1).AddTicks(-1);

            var allInvoices = await _unitOfWork.Invoices.FindAsync(i =>
                i.TenantId == filter.TenantId &&
                i.CreatedAt >= dateFrom &&
                i.CreatedAt <= dateTo);

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                if (Enum.TryParse<InvoiceStatus>(filter.Status, true, out var statusEnum))
                    allInvoices = allInvoices.Where(i => i.Status == statusEnum);
            }

            var invoices = allInvoices.ToList();

            // ✅ جيب الـ OrderIds الـ non-null بس
            var orderIds = invoices
                .Where(i => i.OrderId.HasValue)
                .Select(i => i.OrderId!.Value)
                .ToHashSet();

            var orders = (await _unitOfWork.Orders.FindAsync(o => orderIds.Contains(o.Id)))
                .ToDictionary(o => o.Id);

            var vatRate = tenant.VatRate;
            var lines = new List<VatInvoiceLineDto>();
            decimal totalSales = 0;
            decimal totalSalesExVat = 0;
            decimal totalVatCollected = 0;
            decimal totalDiscount = 0;
            decimal totalShipping = 0;
            int paidCount = 0;
            int unpaidCount = 0;

            foreach (var inv in invoices.OrderBy(i => i.CreatedAt))
            {
                var subTotalIncl = inv.SubTotal;
                var subTotalExVat = Math.Round(subTotalIncl / (1 + vatRate), 2);
                var vatAmount = Math.Round(subTotalIncl - subTotalExVat, 2);

                totalSales += inv.Total;
                totalSalesExVat += subTotalExVat;
                totalVatCollected += vatAmount;
                totalDiscount += inv.Discount;

                // ✅ تحقق إن OrderId مش null قبل ما تستخدمه
                if (inv.OrderId.HasValue && orders.TryGetValue(inv.OrderId.Value, out var order))
                    totalShipping += order.ShippingCost;

                if (inv.Status == InvoiceStatus.Paid) paidCount++;
                else unpaidCount++;

                // ✅ جيب OrderNumber بأمان
                var orderNumber = inv.OrderId.HasValue && orders.TryGetValue(inv.OrderId.Value, out var linkedOrder)
                    ? linkedOrder.OrderNumber
                    : string.Empty;

                lines.Add(new VatInvoiceLineDto
                {
                    InvoiceId = inv.Id,
                    InvoiceNumber = inv.InvoiceNumber,
                    OrderNumber = orderNumber,
                    InvoiceDate = inv.CreatedAt,
                    PaidAt = inv.PaidAt,
                    Status = inv.Status.ToString(),
                    CustomerName = inv.CustomerName,
                    CustomerEmail = inv.CustomerEmail,
                    SubTotal = subTotalExVat,
                    Discount = inv.Discount,
                    VatAmount = vatAmount,
                    Total = inv.Total,
                    VatRate = vatRate
                });
            }

            var monthly = invoices
                .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var groupSubTotal = g.Sum(i => i.SubTotal);
                    var groupExVat = Math.Round(groupSubTotal / (1 + vatRate), 2);
                    var groupVat = Math.Round(groupSubTotal - groupExVat, 2);
                    return new VatMonthlyBreakdownDto
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthLabel = new DateTime(g.Key.Year, g.Key.Month, 1)
                                           .ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                        SalesExVat = groupExVat,
                        VatCollected = groupVat,
                        TotalWithVat = g.Sum(i => i.Total),
                        InvoiceCount = g.Count()
                    };
                })
                .ToList();

            var summary = new VatSummaryDto
            {
                TenantId = filter.TenantId,
                TenantName = tenant.Name,
                VatNumber = tenant.VatNumber ?? string.Empty,
                DateFrom = dateFrom,
                DateTo = filter.DateTo.Date,
                TotalSales = Math.Round(totalSales, 2),
                TotalSalesExVat = Math.Round(totalSalesExVat, 2),
                TotalVatCollected = Math.Round(totalVatCollected, 2),
                TotalDiscount = Math.Round(totalDiscount, 2),
                TotalShipping = Math.Round(totalShipping, 2),
                NetVatPayable = Math.Round(totalVatCollected, 2),
                TotalInvoices = invoices.Count,
                PaidInvoices = paidCount,
                UnpaidInvoices = unpaidCount,
                VatRate = vatRate,
                MonthlyBreakdown = monthly,
                Invoices = lines,
                GeneratedAt = DateTime.UtcNow
            };

            return ApiResponse<VatSummaryDto>.Ok(summary);
        }

        // ════════════════════════════════════════════════════════════════
        // EXPORT — CSV
        // ════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportCsvAsync(TaxReportFilterDto filter)
        {
            var summaryResult = await GetVatSummaryAsync(filter);
            if (!summaryResult.Success)
                return Array.Empty<byte>();

            var summary = summaryResult.Data!;
            var sb = new StringBuilder();

            sb.AppendLine($"VAT Tax Report");
            sb.AppendLine($"Tenant,{Escape(summary.TenantName)}");
            sb.AppendLine($"VAT Number,{Escape(summary.VatNumber)}");
            sb.AppendLine($"Period,{summary.DateFrom:yyyy-MM-dd} to {summary.DateTo:yyyy-MM-dd}");
            sb.AppendLine($"Generated At,{summary.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            sb.AppendLine("SUMMARY");
            sb.AppendLine($"Total Invoices,{summary.TotalInvoices}");
            sb.AppendLine($"Paid Invoices,{summary.PaidInvoices}");
            sb.AppendLine($"Unpaid Invoices,{summary.UnpaidInvoices}");
            sb.AppendLine($"Total Sales (incl. VAT),{summary.TotalSales:F2}");
            sb.AppendLine($"Total Sales (excl. VAT),{summary.TotalSalesExVat:F2}");
            sb.AppendLine($"VAT Rate,{summary.VatRate * 100:F0}%");
            sb.AppendLine($"Total VAT Collected,{summary.TotalVatCollected:F2}");
            sb.AppendLine($"Total Discounts,{summary.TotalDiscount:F2}");
            sb.AppendLine($"Net VAT Payable,{summary.NetVatPayable:F2}");
            sb.AppendLine();

            sb.AppendLine("MONTHLY BREAKDOWN");
            sb.AppendLine("Month,Invoices,Sales (excl. VAT),VAT Collected,Total (incl. VAT)");
            foreach (var m in summary.MonthlyBreakdown)
            {
                sb.AppendLine($"{Escape(m.MonthLabel)},{m.InvoiceCount},{m.SalesExVat:F2},{m.VatCollected:F2},{m.TotalWithVat:F2}");
            }
            sb.AppendLine();

            sb.AppendLine("INVOICE DETAILS");
            sb.AppendLine("Invoice #,Order #,Date,Paid At,Status,Customer,Email,SubTotal (excl. VAT),Discount,VAT Amount,Total (incl. VAT)");
            foreach (var inv in summary.Invoices)
            {
                sb.AppendLine(string.Join(",",
                    Escape(inv.InvoiceNumber),
                    Escape(inv.OrderNumber),
                    inv.InvoiceDate.ToString("yyyy-MM-dd"),
                    inv.PaidAt?.ToString("yyyy-MM-dd") ?? "",
                    Escape(inv.Status),
                    Escape(inv.CustomerName),
                    Escape(inv.CustomerEmail),
                    inv.SubTotal.ToString("F2"),
                    inv.Discount.ToString("F2"),
                    inv.VatAmount.ToString("F2"),
                    inv.Total.ToString("F2")
                ));
            }

            return Encoding.UTF8.GetPreamble().Concat(
                Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        // ════════════════════════════════════════════════════════════════
        // EXPORT — Excel (.xlsx)
        // ════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportExcelAsync(TaxReportFilterDto filter)
        {
            var summaryResult = await GetVatSummaryAsync(filter);
            if (!summaryResult.Success) return Array.Empty<byte>();

            var summary = summaryResult.Data!;
            var sb = new StringBuilder();

            sb.AppendLine("VAT Tax Report\t\t\t");
            sb.AppendLine($"Tenant\t{summary.TenantName}\t\t");
            sb.AppendLine($"VAT Number\t{summary.VatNumber}\t\t");
            sb.AppendLine($"Period\t{summary.DateFrom:yyyy-MM-dd} to {summary.DateTo:yyyy-MM-dd}\t\t");
            sb.AppendLine($"Generated\t{summary.GeneratedAt:yyyy-MM-dd HH:mm} UTC\t\t");
            sb.AppendLine("\t\t\t");

            sb.AppendLine("Metric\tValue\t\t");
            sb.AppendLine($"Total Invoices\t{summary.TotalInvoices}\t\t");
            sb.AppendLine($"Paid\t{summary.PaidInvoices}\t\t");
            sb.AppendLine($"Unpaid\t{summary.UnpaidInvoices}\t\t");
            sb.AppendLine($"Total Sales (incl. VAT)\t{summary.TotalSales:F2}\t\t");
            sb.AppendLine($"Total Sales (excl. VAT)\t{summary.TotalSalesExVat:F2}\t\t");
            sb.AppendLine($"VAT Rate\t{summary.VatRate * 100:F0}%\t\t");
            sb.AppendLine($"Total VAT Collected\t{summary.TotalVatCollected:F2}\t\t");
            sb.AppendLine($"Total Discounts\t{summary.TotalDiscount:F2}\t\t");
            sb.AppendLine($"Net VAT Payable\t{summary.NetVatPayable:F2}\t\t");
            sb.AppendLine("\t\t\t");

            sb.AppendLine("Month\tInvoices\tSales (excl. VAT)\tVAT Collected\tTotal (incl. VAT)");
            foreach (var m in summary.MonthlyBreakdown)
                sb.AppendLine($"{m.MonthLabel}\t{m.InvoiceCount}\t{m.SalesExVat:F2}\t{m.VatCollected:F2}\t{m.TotalWithVat:F2}");

            sb.AppendLine("\t\t\t");
            sb.AppendLine("Invoice #\tOrder #\tDate\tPaid At\tStatus\tCustomer\tEmail\tSales (excl. VAT)\tDiscount\tVAT\tTotal");
            foreach (var inv in summary.Invoices)
                sb.AppendLine($"{inv.InvoiceNumber}\t{inv.OrderNumber}\t{inv.InvoiceDate:yyyy-MM-dd}\t{inv.PaidAt?.ToString("yyyy-MM-dd")}\t{inv.Status}\t{inv.CustomerName}\t{inv.CustomerEmail}\t{inv.SubTotal:F2}\t{inv.Discount:F2}\t{inv.VatAmount:F2}\t{inv.Total:F2}");

            return Encoding.UTF8.GetPreamble().Concat(
                Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        // ════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}