namespace EcomPlatform.Application.DTOs.AdminReports
{
    // ── Stores Report ─────────────────────────────────────────────────────────
    public class StoresReportDto
    {
        public int TotalStores { get; set; }
        public int ActiveStores { get; set; }
        public int SuspendedStores { get; set; }
        public int NewStoresThisMonth { get; set; }
        public int NewStoresLastMonth { get; set; }
        public decimal GrowthRate { get; set; }
        public List<StoreReportItemDto> Stores { get; set; } = new();
        public List<MonthlyCountDto> MonthlyGrowth { get; set; } = new();
    }

    public class StoreReportItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalProducts { get; set; }
        public int TotalCustomers { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── Revenue Report ────────────────────────────────────────────────────────
    public class RevenueReportDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }
        public decimal GrowthRate { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int TotalOrders { get; set; }
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
        public List<TopTenantRevenueDto> TopTenants { get; set; } = new();
    }

    public class MonthlyRevenueDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrdersCount { get; set; }
    }

    public class TopTenantRevenueDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrdersCount { get; set; }
    }

    // ── Orders Report ─────────────────────────────────────────────────────────
    public class OrdersReportDto
    {
        public int TotalOrders { get; set; }
        public int OrdersThisMonth { get; set; }
        public int OrdersLastMonth { get; set; }
        public decimal GrowthRate { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
        public int ReturnedOrders { get; set; }
        public decimal CancellationRate { get; set; }
        public decimal DeliveryRate { get; set; }
        public List<MonthlyCountDto> MonthlyOrders { get; set; } = new();
        public List<OrderStatusBreakdownDto> StatusBreakdown { get; set; } = new();
    }

    public class OrderStatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    // ── Subscriptions Report ──────────────────────────────────────────────────
    public class SubscriptionsReportDto
    {
        public int TotalSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int ExpiredSubscriptions { get; set; }
        public int CancelledSubscriptions { get; set; }
        public int NewSubscriptionsThisMonth { get; set; }
        public int ChurnedThisMonth { get; set; }
        public decimal ChurnRate { get; set; }
        public decimal MonthlyRecurringRevenue { get; set; }
        public decimal AnnualRecurringRevenue { get; set; }
        public List<PlanBreakdownDto> PlanBreakdown { get; set; } = new();
        public List<MonthlyCountDto> MonthlySubscriptions { get; set; } = new();
        public List<ExpiringSubscriptionDto> ExpiringSoon { get; set; } = new();
    }

    public class PlanBreakdownDto
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ExpiringSubscriptionDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public DateTime EndDate { get; set; }
        public int DaysLeft { get; set; }
    }

    // ── Shared ────────────────────────────────────────────────────────────────
    public class MonthlyCountDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // ── Query Params ──────────────────────────────────────────────────────────
    public class ReportQueryParams
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? Year { get; set; }
        public Guid? TenantId { get; set; }
    }
}
