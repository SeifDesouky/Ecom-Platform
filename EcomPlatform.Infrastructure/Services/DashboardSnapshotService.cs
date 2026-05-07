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
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // Get all tenants
            var tenants = await unitOfWork.Tenants.GetAllAsync();

            foreach (var tenant in tenants)
            {
                await TakeTenantSnapshotAsync(unitOfWork, tenant.Id, now, startOfMonth);
            }

            // Platform snapshot
            await TakePlatformSnapshotAsync(unitOfWork, now, startOfMonth);
        }

        private async Task TakeTenantSnapshotAsync(IUnitOfWork unitOfWork,
            Guid tenantId, DateTime now, DateTime startOfMonth)
        {
            var orders = await unitOfWork.Orders.FindAsync(o => o.TenantId == tenantId);
            var ordersList = orders.ToList();

            var customers = await unitOfWork.Customers.FindAsync(c => c.TenantId == tenantId);
            var products = await unitOfWork.Products.FindAsync(p => p.TenantId == tenantId);
            var productsList = products.ToList();

            var snapshot = new DashboardSnapshot
            {
                TenantId = tenantId,
                SnapshotDate = now,
                TotalRevenue = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                    .Sum(o => o.Total),
                RevenueThisMonth = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= startOfMonth)
                    .Sum(o => o.Total),
                TotalOrders = ordersList.Count,
                OrdersThisMonth = ordersList.Count(o => o.CreatedAt >= startOfMonth),
                TotalCustomers = customers.Count(),
                NewCustomersThisMonth = customers.Count(c => c.CreatedAt >= startOfMonth),
                TotalProducts = productsList.Count,
                ActiveProducts = productsList.Count(p => p.IsActive),
                LowStockProducts = productsList.Count(p => p.Stock <= p.LowStockAlert && p.TrackInventory),
                PendingOrders = ordersList.Count(o => o.Status == OrderStatus.Pending),
                ProcessingOrders = ordersList.Count(o => o.Status == OrderStatus.Processing),
                ShippedOrders = ordersList.Count(o => o.Status == OrderStatus.Shipped),
                DeliveredOrders = ordersList.Count(o => o.Status == OrderStatus.Delivered),
                CancelledOrders = ordersList.Count(o => o.Status == OrderStatus.Cancelled)
            };

            await unitOfWork.DashboardSnapshots.AddAsync(snapshot);
            await unitOfWork.SaveChangesAsync();
        }

        private async Task TakePlatformSnapshotAsync(IUnitOfWork unitOfWork,
            DateTime now, DateTime startOfMonth)
        {
            var orders = await unitOfWork.Orders.GetAllAsync();
            var ordersList = orders.ToList();

            var customers = await unitOfWork.Customers.GetAllAsync();
            var products = await unitOfWork.Products.GetAllAsync();
            var productsList = products.ToList();

            var snapshot = new DashboardSnapshot
            {
                TenantId = null,
                SnapshotDate = now,
                TotalRevenue = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                    .Sum(o => o.Total),
                RevenueThisMonth = ordersList
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= startOfMonth)
                    .Sum(o => o.Total),
                TotalOrders = ordersList.Count,
                OrdersThisMonth = ordersList.Count(o => o.CreatedAt >= startOfMonth),
                TotalCustomers = customers.Count(),
                NewCustomersThisMonth = customers.Count(c => c.CreatedAt >= startOfMonth),
                TotalProducts = productsList.Count,
                ActiveProducts = productsList.Count(p => p.IsActive),
                LowStockProducts = productsList.Count(p => p.Stock <= p.LowStockAlert && p.TrackInventory),
                PendingOrders = ordersList.Count(o => o.Status == OrderStatus.Pending),
                ProcessingOrders = ordersList.Count(o => o.Status == OrderStatus.Processing),
                ShippedOrders = ordersList.Count(o => o.Status == OrderStatus.Shipped),
                DeliveredOrders = ordersList.Count(o => o.Status == OrderStatus.Delivered),
                CancelledOrders = ordersList.Count(o => o.Status == OrderStatus.Cancelled)
            };

            await unitOfWork.DashboardSnapshots.AddAsync(snapshot);
            await unitOfWork.SaveChangesAsync();
        }
    }
}