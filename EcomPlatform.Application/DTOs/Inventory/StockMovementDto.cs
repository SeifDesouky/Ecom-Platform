using EcomPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Inventory
{
    // ── Request DTOs ──────────────────────────────────────────────────────────
    public class CreateStockMovementDto
    {
        [Required]
        public StockMovementType Type { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid WarehouseId { get; set; }

        // للنقل بين مستودعين فقط
        public Guid? FromWarehouseId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public decimal? UnitCost { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public Guid? OrderId { get; set; }
        public Guid? CreatedById { get; set; }
        public Guid TenantId { get; set; }
    }

    public class StockAdjustmentDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid WarehouseId { get; set; }

        [Required]
        public int NewQuantity { get; set; }   // الرصيد الجديد بعد الجرد

        public string Notes { get; set; } = string.Empty;
        public Guid? CreatedById { get; set; }
        public Guid TenantId { get; set; }
    }

    public class StockTransferDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid FromWarehouseId { get; set; }

        [Required]
        public Guid ToWarehouseId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public string Notes { get; set; } = string.Empty;
        public Guid? CreatedById { get; set; }
        public Guid TenantId { get; set; }
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────
    public class StockMovementResponseDto
    {
        public Guid Id { get; set; }
        public StockMovementType Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public decimal? UnitCost { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;

        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;

        public Guid? FromWarehouseId { get; set; }
        public string? FromWarehouseName { get; set; }

        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }

        public string CreatedByName { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductStockSummaryDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public int TotalStock { get; set; }
        public int LowStockAlert { get; set; }
        public bool IsLowStock { get; set; }
        public List<WarehouseStockDto> WarehouseBreakdown { get; set; } = new();
    }

    public class WarehouseStockDto
    {
        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public int Stock { get; set; }
    }
}
