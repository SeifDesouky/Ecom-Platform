namespace EcomPlatform.Application.DTOs.Dashboard
{
    public class DashboardStatsDto
    {
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
        public List<RecentOrderDto> RecentOrders { get; set; } = new();
        public List<TopProductDto> TopProducts { get; set; } = new();
        public List<MonthlySalesDto> MonthlySales { get; set; } = new();
    }

    public class RecentOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TopProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public int Stock { get; set; }
    }

    public class MonthlySalesDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }
}