using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EcomPlatform.Infrastructure.Services
{
    public class DashboardSnapshotService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DashboardSnapshotService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public DashboardSnapshotService(
            IServiceProvider serviceProvider,
            ILogger<DashboardSnapshotService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dashboard Snapshot Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TakeSnapshotsAsync();
                    _logger.LogInformation("Dashboard snapshots taken at {time}", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error taking dashboard snapshots");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task TakeSnapshotsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // جيب الـ tenants بـ query واحدة خفيفة
            var tenantIds = await db.Tenants
                .IgnoreQueryFilters()
                .Where(t => !t.IsDeleted)
                .Select(t => t.Id)
                .ToListAsync();

            foreach (var tenantId in tenantIds)
            {
                await TakeTenantSnapshotAsync(db, tenantId, now, startOfMonth);
            }

            await TakePlatformSnapshotAsync(db, now, startOfMonth);
        }

        private async Task TakeTenantSnapshotAsync(AppDbContext db,
            Guid tenantId, DateTime now, DateTime startOfMonth)
        {
            // aggregate مباشرة في الـ DB بدل ما نجيب كل الداتا في الميموري
            var orders = db.Orders.IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId && !o.IsDeleted);

            var totalRevenue = await orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                .SumAsync(o => o.Total);

            var revenueThisMonth = await orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= startOfMonth)
                .SumAsync(o => o.Total);

            var totalOrders = await orders.CountAsync();
            var ordersThisMonth = await orders.CountAsync(o => o.CreatedAt >= startOfMonth);
            var pendingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Pending);
            var processingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Processing);
            var shippedOrders = await orders.CountAsync(o => o.Status == OrderStatus.Shipped);
            var deliveredOrders = await orders.CountAsync(o => o.Status == OrderStatus.Delivered);
            var cancelledOrders = await orders.CountAsync(o => o.Status == OrderStatus.Cancelled);

            var customers = db.Customers.IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId && !c.IsDeleted);

            var totalCustomers = await customers.CountAsync();
            var newCustomersThisMonth = await customers.CountAsync(c => c.CreatedAt >= startOfMonth);

            var products = db.Products.IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted);

            var totalProducts = await products.CountAsync();
            var activeProducts = await products.CountAsync(p => p.IsActive);
            var lowStockProducts = await products.CountAsync(p => p.Stock <= p.LowStockAlert && p.TrackInventory);

            var snapshot = new DashboardSnapshot
            {
                TenantId = tenantId,
                SnapshotDate = now,
                TotalRevenue = totalRevenue,
                RevenueThisMonth = revenueThisMonth,
                TotalOrders = totalOrders,
                OrdersThisMonth = ordersThisMonth,
                TotalCustomers = totalCustomers,
                NewCustomersThisMonth = newCustomersThisMonth,
                TotalProducts = totalProducts,
                ActiveProducts = activeProducts,
                LowStockProducts = lowStockProducts,
                PendingOrders = pendingOrders,
                ProcessingOrders = processingOrders,
                ShippedOrders = shippedOrders,
                DeliveredOrders = deliveredOrders,
                CancelledOrders = cancelledOrders
            };

            await db.DashboardSnapshots.AddAsync(snapshot);
            await db.SaveChangesAsync();
        }

        private async Task TakePlatformSnapshotAsync(AppDbContext db,
            DateTime now, DateTime startOfMonth)
        {
            var orders = db.Orders.IgnoreQueryFilters().Where(o => !o.IsDeleted);

            var totalRevenue = await orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                .SumAsync(o => o.Total);

            var revenueThisMonth = await orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= startOfMonth)
                .SumAsync(o => o.Total);

            var totalOrders = await orders.CountAsync();
            var ordersThisMonth = await orders.CountAsync(o => o.CreatedAt >= startOfMonth);
            var pendingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Pending);
            var processingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Processing);
            var shippedOrders = await orders.CountAsync(o => o.Status == OrderStatus.Shipped);
            var deliveredOrders = await orders.CountAsync(o => o.Status == OrderStatus.Delivered);
            var cancelledOrders = await orders.CountAsync(o => o.Status == OrderStatus.Cancelled);

            var customers = db.Customers.IgnoreQueryFilters().Where(c => !c.IsDeleted);
            var totalCustomers = await customers.CountAsync();
            var newCustomersThisMonth = await customers.CountAsync(c => c.CreatedAt >= startOfMonth);

            var products = db.Products.IgnoreQueryFilters().Where(p => !p.IsDeleted);
            var totalProducts = await products.CountAsync();
            var activeProducts = await products.CountAsync(p => p.IsActive);
            var lowStockProducts = await products.CountAsync(p => p.Stock <= p.LowStockAlert && p.TrackInventory);

            var snapshot = new DashboardSnapshot
            {
                TenantId = null,
                SnapshotDate = now,
                TotalRevenue = totalRevenue,
                RevenueThisMonth = revenueThisMonth,
                TotalOrders = totalOrders,
                OrdersThisMonth = ordersThisMonth,
                TotalCustomers = totalCustomers,
                NewCustomersThisMonth = newCustomersThisMonth,
                TotalProducts = totalProducts,
                ActiveProducts = activeProducts,
                LowStockProducts = lowStockProducts,
                PendingOrders = pendingOrders,
                ProcessingOrders = processingOrders,
                ShippedOrders = shippedOrders,
                DeliveredOrders = deliveredOrders,
                CancelledOrders = cancelledOrders
            };

            await db.DashboardSnapshots.AddAsync(snapshot);
            await db.SaveChangesAsync();
        }
    }
}