using EcomPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Returns
{
    public class CreateReturnRequestDto
    {
        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public ReturnReason Reason { get; set; }

        public string ReasonNote { get; set; } = string.Empty;

        [Required, MinLength(1)]
        public List<CreateReturnItemDto> Items { get; set; } = new();

        public ReturnInitiator Initiator { get; set; } = ReturnInitiator.Customer;

        [Required]
        public Guid TenantId { get; set; }
    }

    public class CreateReturnItemDto
    {
        [Required]
        public Guid OrderItemId { get; set; }

        [Range(1, int.MaxValue)]
        public int QuantityRequested { get; set; } = 1;
    }

    public class ReviewReturnRequestDto
    {
        [Required]
        public bool Approved { get; set; }

        public string Note { get; set; } = string.Empty;

        public List<ApproveReturnItemDto> ApprovedItems { get; set; } = new();

        [Required]
        public Guid ReviewedById { get; set; }
    }

    public class ApproveReturnItemDto
    {
        [Required]
        public Guid ReturnItemId { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantityApproved { get; set; }
    }

    public class ProcessRefundDto
    {
        [Required]
        public Guid ReturnRequestId { get; set; }

        [Required]
        public RefundMethod Method { get; set; }

        public string GatewayTransactionId { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        [Required]
        public Guid ProcessedById { get; set; }
    }

    public class ReturnRequestResponseDto
    {
        public Guid Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;

        // ── بيانات العميل ─────────────────────────────────────────────────
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public ReturnInitiator Initiator { get; set; }
        public string InitiatorName { get; set; } = string.Empty;

        public ReturnReason Reason { get; set; }
        public string ReasonName { get; set; } = string.Empty;
        public string ReasonNote { get; set; } = string.Empty;

        public ReturnStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;

        public decimal RequestedAmount { get; set; }
        public decimal ApprovedAmount { get; set; }

        public RefundStatus RefundStatus { get; set; }
        public string RefundStatusName { get; set; } = string.Empty;
        public RefundMethod RefundMethod { get; set; }
        public string RefundMethodName { get; set; } = string.Empty;
        public DateTime? RefundedAt { get; set; }
        public string RefundNote { get; set; } = string.Empty;

        public bool StockRestored { get; set; }

        public string ReviewedByName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }

        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<ReturnItemResponseDto> Items { get; set; } = new();
    }

    public class ReturnItemResponseDto
    {
        public Guid Id { get; set; }
        public Guid OrderItemId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public int QuantityRequested { get; set; }
        public int QuantityApproved { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}