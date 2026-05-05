using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Dashboard;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DashboardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<DashboardStatsDto>> GetTenantStatsAsync(Guid tenantId)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // Orders
            var orders = await _unitOfWork.Orders.FindAsync(o => o.TenantId == tenantId);
            var ordersList = orders.ToList();

            // Customers
            var customers = await _unitOfWork.Customers.FindAsync(c => c.TenantId == tenantId);
            var customersList = customers.ToList();

            // Products
            var products = await _unitOfWork.Products.FindAsync(p => p.TenantId == tenantId);
            var productsList = products.ToList();

            // Order Items
            var allOrderIds = ordersList.Select(o => o.Id).ToList();
            var orderItems = await _unitOfWork.OrderItems.FindAsync(i => allOrderIds.Contains(i.OrderId));
            var orderItemsList = orderItems.ToList();

            // Stats
            var stats = new DashboardStatsDto
            {
                // Revenue
                TotalRevenue = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                    .Sum(o => o.Total),
                RevenueThisMonth = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= startOfMonth)
                    .Sum(o => o.Total),

                // Orders
                TotalOrders = ordersList.Count,
                OrdersThisMonth = ordersList.Count(o => o.CreatedAt >= startOfMonth),
                PendingOrders = ordersList.Count(o => o.Status == OrderStatus.Pending),
                ProcessingOrders = ordersList.Count(o => o.Status == OrderStatus.Processing),
                ShippedOrders = ordersList.Count(o => o.Status == OrderStatus.Shipped),
                DeliveredOrders = ordersList.Count(o => o.Status == OrderStatus.Delivered),
                CancelledOrders = ordersList.Count(o => o.Status == OrderStatus.Cancelled),

                // Customers
                TotalCustomers = customersList.Count,
                NewCustomersThisMonth = customersList.Count(c => c.CreatedAt >= startOfMonth),

                // Products
                TotalProducts = productsList.Count,
                ActiveProducts = productsList.Count(p => p.IsActive),
                LowStockProducts = productsList.Count(p => p.Stock <= p.LowStockAlert && p.TrackInventory),

                // Recent Orders
                RecentOrders = ordersList
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .Select(o => new RecentOrderDto
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber,
                        CustomerName = o.CustomerName,
                        Total = o.Total,
                        Status = (int)o.Status,
                        CreatedAt = o.CreatedAt
                    }).ToList(),

                // Top Products
                TopProducts = orderItemsList
                    .GroupBy(i => i.ProductId)
                    .Select(g => new TopProductDto
                    {
                        Id = g.Key,
                        Name = g.First().ProductName,
                        SKU = g.First().ProductSKU,
                        TotalSold = g.Sum(i => i.Quantity),
                        TotalRevenue = g.Sum(i => i.TotalPrice),
                        Stock = productsList.FirstOrDefault(p => p.Id == g.Key)?.Stock ?? 0
                    })
                    .OrderByDescending(p => p.TotalSold)
                    .Take(5)
                    .ToList(),

                // Monthly Sales (last 6 months)
                MonthlySales = Enumerable.Range(0, 6)
                    .Select(i =>
                    {
                        var month = now.AddMonths(-i);
                        var monthStart = new DateTime(month.Year, month.Month, 1);
                        var monthEnd = monthStart.AddMonths(1);
                        return new MonthlySalesDto
                        {
                            Month = month.ToString("MMM yyyy"),
                            Revenue = ordersList
                                .Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd
                                    && o.PaymentStatus == PaymentStatus.Paid)
                                .Sum(o => o.Total),
                            Orders = ordersList
                                .Count(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd)
                        };
                    })
                    .Reverse()
                    .ToList()
            };

            return ApiResponse<DashboardStatsDto>.Ok(stats);
        }

        public async Task<ApiResponse<DashboardStatsDto>> GetPlatformStatsAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var orders = await _unitOfWork.Orders.GetAllAsync();
            var ordersList = orders.ToList();

            var customers = await _unitOfWork.Customers.GetAllAsync();
            var customersList = customers.ToList();

            var products = await _unitOfWork.Products.GetAllAsync();
            var productsList = products.ToList();

            var tenants = await _unitOfWork.Tenants.GetAllAsync();

            var stats = new DashboardStatsDto
            {
                TotalRevenue = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                    .Sum(o => o.Total),
                RevenueThisMonth = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= startOfMonth)
                    .Sum(o => o.Total),
                TotalOrders = ordersList.Count,
                OrdersThisMonth = ordersList.Count(o => o.CreatedAt >= startOfMonth),
                TotalCustomers = customersList.Count,
                NewCustomersThisMonth = customersList.Count(c => c.CreatedAt >= startOfMonth),
                TotalProducts = productsList.Count,
                ActiveProducts = productsList.Count(p => p.IsActive),
                LowStockProducts = productsList.Count(p => p.Stock <= p.LowStockAlert && p.TrackInventory),
                PendingOrders = ordersList.Count(o => o.Status == OrderStatus.Pending),
                ProcessingOrders = ordersList.Count(o => o.Status == OrderStatus.Processing),
                ShippedOrders = ordersList.Count(o => o.Status == OrderStatus.Shipped),
                DeliveredOrders = ordersList.Count(o => o.Status == OrderStatus.Delivered),
                CancelledOrders = ordersList.Count(o => o.Status == OrderStatus.Cancelled),
                RecentOrders = ordersList
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .Select(o => new RecentOrderDto
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber,
                        CustomerName = o.CustomerName,
                        Total = o.Total,
                        Status = (int)o.Status,
                        CreatedAt = o.CreatedAt
                    }).ToList(),
                MonthlySales = Enumerable.Range(0, 6)
                    .Select(i =>
                    {
                        var month = now.AddMonths(-i);
                        var monthStart = new DateTime(month.Year, month.Month, 1);
                        var monthEnd = monthStart.AddMonths(1);
                        return new MonthlySalesDto
                        {
                            Month = month.ToString("MMM yyyy"),
                            Revenue = ordersList
                                .Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd
                                    && o.PaymentStatus == PaymentStatus.Paid)
                                .Sum(o => o.Total),
                            Orders = ordersList
                                .Count(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd)
                        };
                    })
                    .Reverse()
                    .ToList()
            };

            return ApiResponse<DashboardStatsDto>.Ok(stats);
        }
    }
}