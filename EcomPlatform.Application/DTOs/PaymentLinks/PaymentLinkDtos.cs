using EcomPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.PaymentLinks
{
    // ══════════════════════════════════════════════════════════════════════
    // REQUEST DTOs
    // ══════════════════════════════════════════════════════════════════════

    public class CreatePaymentLinkDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public PaymentLinkType LinkType { get; set; } = PaymentLinkType.FreeAmount;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        public string Currency { get; set; } = "SAR";

        // لو OrderBased
        public Guid? OrderId { get; set; }

        // لو ProductBased
        public List<PaymentLinkItemDto> Items { get; set; } = new();

        // Expiry
        public DateTime? ExpiresAt { get; set; }
        public int? MaxUses { get; set; }

        // Redirect
        public string SuccessRedirectUrl { get; set; } = string.Empty;
        public string FailureRedirectUrl { get; set; } = string.Empty;

        public string Metadata { get; set; } = string.Empty;

        [Required]
        public Guid TenantId { get; set; }
        public Guid? CreatedById { get; set; }
    }

    public class PaymentLinkItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }

    public class UpdatePaymentLinkDto
    {
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public int? MaxUses { get; set; }
        public string SuccessRedirectUrl { get; set; } = string.Empty;
        public string FailureRedirectUrl { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
    }

    public class ProcessPaymentDto
    {
        [Required]
        public string LinkCode { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string PayerName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string PayerEmail { get; set; } = string.Empty;

        public string PayerPhone { get; set; } = string.Empty;

        [Required]
        public string GatewayName { get; set; } = string.Empty;

        [Required]
        public string GatewayTransactionId { get; set; } = string.Empty;

        public string GatewayResponse { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════════
    // RESPONSE DTOs
    // ══════════════════════════════════════════════════════════════════════

    public class PaymentLinkResponseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public PaymentLinkType LinkType { get; set; }
        public string LinkTypeName { get; set; } = string.Empty;
        public PaymentLinkStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;

        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }

        public DateTime? ExpiresAt { get; set; }
        public int? MaxUses { get; set; }
        public int UsedCount { get; set; }
        public bool IsExpired { get; set; }

        public string SuccessRedirectUrl { get; set; } = string.Empty;
        public string FailureRedirectUrl { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;

        public string CreatedByName { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<PaymentLinkItemResponseDto> Items { get; set; } = new();
        public List<PaymentLinkTransactionResponseDto> Transactions { get; set; } = new();

        // الرابط الكامل للمشاركة
        public string PublicUrl { get; set; } = string.Empty;
    }

    public class PaymentLinkItemResponseDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => UnitPrice * Quantity;
    }

    public class PaymentLinkTransactionResponseDto
    {
        public Guid Id { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public string PayerEmail { get; set; } = string.Empty;
        public string PayerPhone { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string GatewayName { get; set; } = string.Empty;
        public string GatewayTransactionId { get; set; } = string.Empty;
        public Guid? GeneratedOrderId { get; set; }
        public DateTime? PaidAt { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentLinkPublicDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public PaymentLinkType LinkType { get; set; }
        public bool IsValid { get; set; }
        public string InvalidReason { get; set; } = string.Empty;
        public List<PaymentLinkItemResponseDto> Items { get; set; } = new();
    }
}
