using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Invoices;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;

        public InvoiceService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GenerateFromOrderAsync(Guid orderId)
        {
            // Check if invoice already exists
            var existing = await _unitOfWork.Invoices.FindAsync(i => i.OrderId == orderId);
            if (existing.Any())
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice already exists for this order");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Order not found");

            var orderItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId);

            var invoice = new Invoice
            {
                InvoiceNumber = GenerateInvoiceNumber(),
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
                OrderId = orderId,
                Items = orderItems.Select(i => new InvoiceItem
                {
                    Description = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };

            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            invoice.Order = order;

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice), "Invoice generated successfully");
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetByIdAsync(Guid id)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            if (invoice == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == id);
            invoice.Items = items.ToList();

            var order = await _unitOfWork.Orders.GetByIdAsync(invoice.OrderId);
            invoice.Order = order;

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice));
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetByOrderIdAsync(Guid orderId)
        {
            var invoices = await _unitOfWork.Invoices.FindAsync(i => i.OrderId == orderId);
            var invoice = invoices.FirstOrDefault();

            if (invoice == null)
                return ApiResponse<InvoiceResponseDto>.Fail("Invoice not found");

            var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoice.Id);
            invoice.Items = items.ToList();

            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            invoice.Order = order;

            return ApiResponse<InvoiceResponseDto>.Ok(MapToDto(invoice));
        }

        public async Task<ApiResponse<IEnumerable<InvoiceResponseDto>>> GetAllByTenantAsync(Guid tenantId)
        {
            var invoices = await _unitOfWork.Invoices.FindAsync(i => i.TenantId == tenantId);
            var invoicesList = invoices.ToList();

            foreach (var invoice in invoicesList)
            {
                var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoice.Id);
                invoice.Items = items.ToList();
            }

            return ApiResponse<IEnumerable<InvoiceResponseDto>>.Ok(invoicesList.Select(MapToDto));
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

        private static string GenerateInvoiceNumber() =>
            $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

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
            TenantId = invoice.TenantId,
            OrderId = invoice.OrderId,
            OrderNumber = invoice.Order?.OrderNumber ?? string.Empty,
            CreatedAt = invoice.CreatedAt,
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