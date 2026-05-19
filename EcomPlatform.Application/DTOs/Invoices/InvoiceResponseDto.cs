using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Invoices
{
    public class InvoiceResponseDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public InvoiceStatus Status { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string QrCodeBase64 { get; set; } = string.Empty;
        public List<InvoiceItemResponseDto> Items { get; set; } = new();
    }

    public class InvoiceItemResponseDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}