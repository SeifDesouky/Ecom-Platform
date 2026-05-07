using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class DashboardSnapshot : BaseEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public int TotalOrders { get; set; }
        public int OrdersThisMonth { get; set; }
        public int TotalCustomers { get; set; }
        public int NewCustomersThisMonth { get; set; }
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int LowStockProducts { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
        public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    }
}