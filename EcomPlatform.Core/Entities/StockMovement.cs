using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class StockMovement : BaseEntity, ITenantEntity
    {
        public StockMovementType Type { get; set; }

        public int Quantity { get; set; }          // موجب = دخول، سالب = خروج
        public int QuantityBefore { get; set; }    // الرصيد قبل الحركة
        public int QuantityAfter { get; set; }     // الرصيد بعد الحركة

        public decimal? UnitCost { get; set; }     // تكلفة الوحدة (للمشتريات)
        public string Reference { get; set; } = string.Empty;  // رقم الأوردر أو الفاتورة
        public string Notes { get; set; } = string.Empty;

        // من أي مستودع (للنقل)
        public Guid? FromWarehouseId { get; set; }
        public Warehouse? FromWarehouse { get; set; }

        // إلى أي مستودع
        public Guid WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        // المنتج
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        // الأوردر المرتبط (لو Sale أو Return)
        public Guid? OrderId { get; set; }
        public Order? Order { get; set; }

        // من نفّذ الحركة
        public Guid? CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        // Tenant
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
