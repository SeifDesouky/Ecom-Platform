using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Invoices;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EcomPlatform.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IAccountingService _accountingService;

        public InvoiceService(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IConfiguration configuration,
            IAccountingService accountingService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _accountingService = accountingService;
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GenerateFromOrderAsync(Guid orderId)
        {
            // ✅ تحقق إن الفاتورة مش موجودة للـ Order العادي
            var existingForOrder = await _unitOfWork.Invoices.FindAsync(i => i.OrderId.HasValue && i.OrderId.Value == orderId);
            if (existingForOrder.Any())
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice already exists for this order");

            // ✅ تحقق إن الفاتورة مش موجودة للـ POS Order
            var existingForPosOrder = await _unitOfWork.Invoices.FindAsync(i => i.PosOrderId == orderId);
            if (existingForPosOrder.Any())
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice already exists for this order");

            // ✅ دور في الـ Orders العادية الأول
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);

            if (order != null)
            {
                // ─── Normal Order ────────────────────────────────────────────
                var orderItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId);
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(order.TenantId ?? Guid.Empty);

                var invoice = new Invoice
                {
                    InvoiceNumber = await GenerateInvoiceNumberAsync(),
                    Status = order.PaymentStatus == PaymentStatus.Paid
                        ? InvoiceStatus.Paid
                        : InvoiceStatus.Unpaid,
                    SubTotal = order.SubTotal,
                    Tax = order.Tax,
                    Discount = order.Discount,
                    Total = order.Total,
                    DueDate = DateTime.UtcNow.AddDays(7),
                    PaidAt = order.PaidAt,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    CustomerPhone = order.CustomerPhone,
                    CustomerAddress = $"{order.ShippingAddress}, {order.ShippingCity}, {order.ShippingCountry}",
                    TenantId = order.TenantId,
                    OrderId = orderId,       // ✅ Order عادي
                    PosOrderId = null,       // ✅ مش POS
                    Items = orderItems.Select(i => new InvoiceItem
                    {
                        Description = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice
                    }).ToList()
                };

                var vatRate = 0.15m;
                var subtotalExVat = invoice.SubTotal / (1 + vatRate);
                var vatAmount = invoice.SubTotal - subtotalExVat;
                invoice.QrCodeBase64 = GenerateQrCode(invoice, tenant!, subtotalExVat, vatAmount);
                invoice.ZatcaXml = GenerateZatcaXml(invoice, orderItems.ToList(), tenant, subtotalExVat, vatAmount, invoice.Total);

                await _unitOfWork.Invoices.AddAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

                invoice.Order = order;

                if (!string.IsNullOrEmpty(invoice.CustomerEmail))
                {
                    _ = _emailService.SendInvoiceAsync(
                        invoice.CustomerEmail,
                        invoice.CustomerName,
                        invoice.InvoiceNumber,
                        invoice.Total,
                        invoice.DueDate);
                }

                return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice), "Invoice generated successfully");
            }

            // ✅ لو مش Order عادي — دور في POS Orders
            var posOrder = await _unitOfWork.PosOrders.GetByIdAsync(orderId);
            if (posOrder == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Order not found");

            // ✅ جيب POS Order Items
            var posOrderItems = await _unitOfWork.PosOrderItems.FindAsync(i => i.PosOrderId == posOrder.Id);
            var posTenant = posOrder.TenantId.HasValue
                ? await _unitOfWork.Tenants.GetByIdAsync(posOrder.TenantId.Value)
                : null;

            var posInvoice = new Invoice
            {
                InvoiceNumber = await GenerateInvoiceNumberAsync(),
                Status = InvoiceStatus.Paid, // POS دايماً مدفوع في الحال
                SubTotal = posOrder.SubTotal,
                Tax = posOrder.TaxAmount,
                Discount = posOrder.DiscountAmount,
                Total = posOrder.Total,
                DueDate = DateTime.UtcNow,
                PaidAt = posOrder.CreatedAt,
                CustomerName = string.IsNullOrWhiteSpace(posOrder.CustomerName)
                    ? "عميل نقدي"
                    : posOrder.CustomerName,
                CustomerEmail = string.Empty,
                CustomerPhone = posOrder.CustomerPhone ?? string.Empty,
                CustomerAddress = string.Empty,
                TenantId = posOrder.TenantId,
                OrderId = null,              // ✅ مش Order عادي
                PosOrderId = posOrder.Id,    // ✅ POS Order
                Items = posOrderItems.Select(i => new InvoiceItem
                {
                    Description = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };

            var posVatRate = 0.15m;
            var posSubtotalExVat = posInvoice.SubTotal / (1 + posVatRate);
            var posVatAmount = posInvoice.SubTotal - posSubtotalExVat;
            posInvoice.QrCodeBase64 = GenerateQrCode(posInvoice, posTenant!, posSubtotalExVat, posVatAmount);
            posInvoice.ZatcaXml = GenerateZatcaXml(
                posInvoice,
                posOrderItems.Select(i => new OrderItem
                {
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList(),
                posTenant,
                posSubtotalExVat,
                posVatAmount,
                posInvoice.Total
            );

            await _unitOfWork.Invoices.AddAsync(posInvoice);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(posInvoice), "Invoice generated successfully");
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetByIdAsync(Guid id)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == id);
            invoice.Items = items.ToList();

            if (invoice.OrderId.HasValue)
            {
                var order = await _unitOfWork.Orders.GetByIdAsync(invoice.OrderId.Value);
                invoice.Order = order;
            }

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice));
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetByCustomerAsync(Guid id, Guid customerId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            if (!invoice.OrderId.HasValue)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            var order = await _unitOfWork.Orders.GetByIdAsync(invoice.OrderId.Value);
            if (order == null || order.CustomerId != customerId)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == id);
            invoice.Items = items.ToList();
            invoice.Order = order;

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice));
        }

        public async Task<ApiResponse<PagedResponse<InvoiceResponseDto>>> GetAllByCustomerAsync(Guid customerId, PaginationParams pagination)
        {
            var orders = await _unitOfWork.Orders.FindAsync(o => o.CustomerId == customerId);
            var orderIds = orders.Select(o => o.Id).ToHashSet();
            var orderMap = orders.ToDictionary(o => o.Id);
            var all = await _unitOfWork.Invoices.FindAsync(i => i.OrderId.HasValue && orderIds.Contains(i.OrderId.Value));
            var totalCount = all.Count();

            var paged = all
                .OrderByDescending(i => i.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToList();

            foreach (var invoice in paged)
            {
                var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoice.Id);
                invoice.Items = items.ToList();
                if (invoice.OrderId.HasValue)
                    if (orderMap.TryGetValue(invoice.OrderId.Value, out var mappedOrder)) invoice.Order = mappedOrder;
            }

            var result = PagedResponse<InvoiceResponseDto>.Create(paged.Select(MapToDto).ToList(), totalCount, pagination);
            return ApiResponse<PagedResponse<InvoiceResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetByOrderIdAsync(Guid orderId)
        {
            var invoices = await _unitOfWork.Invoices.FindAsync(i => i.OrderId.HasValue && i.OrderId.Value == orderId);
            var invoice = invoices.FirstOrDefault();

            if (invoice == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoice.Id);
            invoice.Items = items.ToList();

            if (invoice.OrderId.HasValue)
            {
                var order = await _unitOfWork.Orders.GetByIdAsync(invoice.OrderId.Value);
                invoice.Order = order;
            }

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice));
        }

        public async Task<ApiResponse<PagedResponse<InvoiceResponseDto>>> GetAllByTenantAsync(Guid tenantId, PaginationParams pagination)
        {
            var all = await _unitOfWork.Invoices.FindAsync(i => i.TenantId == tenantId);
            var totalCount = all.Count();

            var paged = all
                .OrderByDescending(i => i.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToList();

            foreach (var invoice in paged)
            {
                var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoice.Id);
                invoice.Items = items.ToList();
            }

            var result = PagedResponse<InvoiceResponseDto>.Create(paged.Select(MapToDto).ToList(), totalCount, pagination);
            return ApiResponse<PagedResponse<InvoiceResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, InvoiceStatus status)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
                return ApiResponse<bool>.Fail("Invoice not found");

            invoice.Status = status;
            if (status == InvoiceStatus.Paid)
                invoice.PaidAt = DateTime.UtcNow;

            await _unitOfWork.Invoices.UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            if (status == InvoiceStatus.Paid && invoice.TenantId.HasValue)
                await _accountingService.CreateInvoicePaidEntryAsync(invoice.Id, invoice.TenantId.Value);

            return ApiResponse<bool>.Ok(true, "Invoice status updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
                return ApiResponse<bool>.Fail("Invoice not found");

            await _unitOfWork.Invoices.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Invoice deleted successfully");
        }

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            var all = await _unitOfWork.Invoices.GetAllAsync();
            var nextNumber = 1000 + all.Count() + 1;
            return $"INV-{nextNumber}";
        }

        private static string GenerateQrCode(Invoice invoice, Tenant tenant, decimal subtotalExVat, decimal vatAmount)
        {
            var sellerName = tenant?.Name ?? string.Empty;
            var vatNumber = tenant?.VatNumber ?? string.Empty;
            var invoiceDate = (invoice.CreatedAt == default ? DateTime.UtcNow : invoice.CreatedAt)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");
            var totalWithVat = invoice.Total.ToString("F2");
            var vatAmountStr = vatAmount.ToString("F2");

            var tlvBytes = new List<byte>();

            void AppendTlv(byte tag, string value)
            {
                var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
                tlvBytes.Add(tag);
                tlvBytes.Add((byte)valueBytes.Length);
                tlvBytes.AddRange(valueBytes);
            }

            AppendTlv(1, sellerName);
            AppendTlv(2, vatNumber);
            AppendTlv(3, invoiceDate);
            AppendTlv(4, totalWithVat);
            AppendTlv(5, vatAmountStr);

            return Convert.ToBase64String(tlvBytes.ToArray());
        }

        private static string GenerateZatcaXml(Invoice invoice, List<OrderItem> orderItems, Tenant? tenant, decimal subtotalExVat, decimal vatAmount, decimal total)
        {
            var invoiceDate = invoice.CreatedAt == default ? DateTime.UtcNow : invoice.CreatedAt;
            var sellerName = tenant?.Name ?? string.Empty;
            var vatNumber = tenant?.VatNumber ?? string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"");
            sb.AppendLine("         xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"");
            sb.AppendLine("         xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">");
            sb.AppendLine($"  <cbc:ID>{invoice.InvoiceNumber}</cbc:ID>");
            sb.AppendLine($"  <cbc:IssueDate>{invoiceDate:yyyy-MM-dd}</cbc:IssueDate>");
            sb.AppendLine($"  <cbc:IssueTime>{invoiceDate:HH:mm:ss}</cbc:IssueTime>");
            sb.AppendLine("  <cbc:InvoiceTypeCode name=\"0100000000000000\">388</cbc:InvoiceTypeCode>");
            sb.AppendLine("  <cbc:DocumentCurrencyCode>SAR</cbc:DocumentCurrencyCode>");
            sb.AppendLine("  <cac:AccountingSupplierParty><cac:Party><cac:PartyName>");
            sb.AppendLine($"    <cbc:Name>{System.Security.SecurityElement.Escape(sellerName)}</cbc:Name>");
            sb.AppendLine("  </cac:PartyName><cac:PartyTaxScheme>");
            sb.AppendLine($"    <cbc:CompanyID>{vatNumber}</cbc:CompanyID>");
            sb.AppendLine("    <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>");
            sb.AppendLine("  </cac:PartyTaxScheme></cac:Party></cac:AccountingSupplierParty>");
            sb.AppendLine("  <cac:AccountingCustomerParty><cac:Party><cac:PartyName>");
            sb.AppendLine($"    <cbc:Name>{System.Security.SecurityElement.Escape(invoice.CustomerName ?? string.Empty)}</cbc:Name>");
            sb.AppendLine("  </cac:PartyName></cac:Party></cac:AccountingCustomerParty>");
            sb.AppendLine("  <cac:TaxTotal>");
            sb.AppendLine($"    <cbc:TaxAmount currencyID=\"SAR\">{vatAmount:F2}</cbc:TaxAmount>");
            sb.AppendLine("    <cac:TaxSubtotal>");
            sb.AppendLine($"      <cbc:TaxableAmount currencyID=\"SAR\">{subtotalExVat:F2}</cbc:TaxableAmount>");
            sb.AppendLine($"      <cbc:TaxAmount currencyID=\"SAR\">{vatAmount:F2}</cbc:TaxAmount>");
            sb.AppendLine("      <cac:TaxCategory><cbc:ID>S</cbc:ID><cbc:Percent>15</cbc:Percent>");
            sb.AppendLine("        <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>");
            sb.AppendLine("      </cac:TaxCategory></cac:TaxSubtotal></cac:TaxTotal>");
            sb.AppendLine("  <cac:LegalMonetaryTotal>");
            sb.AppendLine($"    <cbc:LineExtensionAmount currencyID=\"SAR\">{subtotalExVat:F2}</cbc:LineExtensionAmount>");
            sb.AppendLine($"    <cbc:TaxExclusiveAmount currencyID=\"SAR\">{subtotalExVat:F2}</cbc:TaxExclusiveAmount>");
            sb.AppendLine($"    <cbc:TaxInclusiveAmount currencyID=\"SAR\">{total:F2}</cbc:TaxInclusiveAmount>");
            sb.AppendLine($"    <cbc:PayableAmount currencyID=\"SAR\">{total:F2}</cbc:PayableAmount>");
            sb.AppendLine("  </cac:LegalMonetaryTotal>");

            int lineId = 1;
            foreach (var item in orderItems)
            {
                var lineSubtotal = item.UnitPrice * item.Quantity;
                sb.AppendLine("  <cac:InvoiceLine>");
                sb.AppendLine($"    <cbc:ID>{lineId++}</cbc:ID>");
                sb.AppendLine($"    <cbc:InvoicedQuantity unitCode=\"PCE\">{item.Quantity}</cbc:InvoicedQuantity>");
                sb.AppendLine($"    <cbc:LineExtensionAmount currencyID=\"SAR\">{lineSubtotal:F2}</cbc:LineExtensionAmount>");
                sb.AppendLine($"    <cac:Item><cbc:Name>{System.Security.SecurityElement.Escape(item.ProductName ?? string.Empty)}</cbc:Name></cac:Item>");
                sb.AppendLine($"    <cac:Price><cbc:PriceAmount currencyID=\"SAR\">{item.UnitPrice:F2}</cbc:PriceAmount></cac:Price>");
                sb.AppendLine("  </cac:InvoiceLine>");
            }

            sb.AppendLine("</Invoice>");
            return sb.ToString();
        }

        private static InvoiceResponseDto MapToDto(Invoice invoice) => new()
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Status = invoice.Status,
            SubTotal = invoice.SubTotal,
            Tax = invoice.Tax,
            Discount = invoice.Discount,
            Total = invoice.Total,
            Notes = invoice.Notes,
            DueDate = invoice.DueDate,
            PaidAt = invoice.PaidAt,
            CustomerName = invoice.CustomerName,
            CustomerEmail = invoice.CustomerEmail,
            CustomerPhone = invoice.CustomerPhone,
            CustomerAddress = invoice.CustomerAddress,
            TenantId = invoice.TenantId ?? Guid.Empty,
            OrderId = invoice.OrderId ?? Guid.Empty,
            OrderNumber = invoice.Order?.OrderNumber ?? invoice.PosOrder?.ReceiptNumber ?? string.Empty,
            CreatedAt = invoice.CreatedAt,
            QrCodeBase64 = invoice.QrCodeBase64,
            Items = invoice.Items?.Select(i => new InvoiceItemResponseDto
            {
                Id = i.Id,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList() ?? new()
        };
    }
}