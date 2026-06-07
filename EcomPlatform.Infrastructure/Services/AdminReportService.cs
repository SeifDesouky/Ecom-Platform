using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.AdminReports;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class AdminReportService : IAdminReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ── Stores Report ─────────────────────────────────────────────────────
        public async Task<ApiResponse<StoresReportDto>> GetStoresReportAsync(ReportQueryParams query)
        {
            var allTenants = await _unitOfWork.Tenants.GetAllAsync();
            var tenants = allTenants.ToList();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);

            var newThisMonth = tenants.Count(t => t.CreatedAt >= startOfMonth);
            var newLastMonth = tenants.Count(t => t.CreatedAt >= startOfLastMonth && t.CreatedAt < startOfMonth);
            var growthRate = newLastMonth > 0
                ? Math.Round((decimal)(newThisMonth - newLastMonth) / newLastMonth * 100, 2)
                : 0;

            // Monthly growth — آخر 12 شهر
            var monthlyGrowth = new List<MonthlyCountDto>();
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                monthlyGrowth.Add(new MonthlyCountDto
                {
                    Year = monthStart.Year,
                    Month = monthStart.Month,
                    MonthName = monthStart.ToString("MMM yyyy"),
                    Count = tenants.Count(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
                });
            }

            // Store items مع الـ subscriptions
            var storeItems = new List<StoreReportItemDto>();
            foreach (var tenant in tenants.OrderByDescending(t => t.CreatedAt).Take(50))
            {
                var subs = await _unitOfWork.Subscriptions.FindAsync(s => s.TenantId == tenant.Id);
                var activeSub = subs.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
                var orders = await _unitOfWork.Orders.FindAsync(o => o.TenantId == tenant.Id);

                storeItems.Add(new StoreReportItemDto
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Email = tenant.Email,
                    Status = tenant.Status.ToString(),
                    PlanName = activeSub != null
                        ? (await _unitOfWork.Plans.GetByIdAsync(activeSub.PlanId ?? Guid.Empty))?.Name ?? "—"
                        : "—",
                    TotalOrders = orders.Count(),
                    TotalRevenue = orders.Sum(o => o.Total),
                    SubscriptionEndDate = tenant.SubscriptionEndDate,
                    CreatedAt = tenant.CreatedAt
                });
            }

            var result = new StoresReportDto
            {
                TotalStores = tenants.Count,
                ActiveStores = tenants.Count(t => t.IsActive && t.Status == TenantStatus.Active),
                SuspendedStores = tenants.Count(t => t.Status == TenantStatus.Suspended),
                NewStoresThisMonth = newThisMonth,
                NewStoresLastMonth = newLastMonth,
                GrowthRate = growthRate,
                Stores = storeItems,
                MonthlyGrowth = monthlyGrowth
            };

            return ApiResponse<StoresReportDto>.Ok(result);
        }

        // ── Revenue Report ────────────────────────────────────────────────────
        public async Task<ApiResponse<RevenueReportDto>> GetRevenueReportAsync(ReportQueryParams query)
        {
            var allOrders = await _unitOfWork.Orders.GetAllAsync();
            var orders = allOrders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                .ToList();

            if (query.From.HasValue) orders = orders.Where(o => o.CreatedAt >= query.From).ToList();
            if (query.To.HasValue) orders = orders.Where(o => o.CreatedAt <= query.To).ToList();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);

            var thisMonthOrders = orders.Where(o => o.CreatedAt >= startOfMonth).ToList();
            var lastMonthOrders = orders.Where(o => o.CreatedAt >= startOfLastMonth && o.CreatedAt < startOfMonth).ToList();

            var revenueThisMonth = thisMonthOrders.Sum(o => o.Total);
            var revenueLastMonth = lastMonthOrders.Sum(o => o.Total);
            var growthRate = revenueLastMonth > 0
                ? Math.Round((revenueThisMonth - revenueLastMonth) / revenueLastMonth * 100, 2)
                : 0;

            // Monthly Revenue آخر 12 شهر
            var monthlyRevenue = new List<MonthlyRevenueDto>();
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                var monthOrders = orders.Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd).ToList();
                monthlyRevenue.Add(new MonthlyRevenueDto
                {
                    Year = monthStart.Year,
                    Month = monthStart.Month,
                    MonthName = monthStart.ToString("MMM yyyy"),
                    Revenue = monthOrders.Sum(o => o.Total),
                    OrdersCount = monthOrders.Count
                });
            }

            // Top Tenants
            var topTenants = orders
                .GroupBy(o => o.TenantId)
                .Select(g => new TopTenantRevenueDto
                {
                    TenantId = g.Key ?? Guid.Empty,
                    Revenue = g.Sum(o => o.Total),
                    OrdersCount = g.Count()
                })
                .OrderByDescending(t => t.Revenue)
                .Take(10)
                .ToList();

            foreach (var t in topTenants)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(t.TenantId);
                t.TenantName = tenant?.Name ?? "—";
            }

            var result = new RevenueReportDto
            {
                TotalRevenue = orders.Sum(o => o.Total),
                RevenueThisMonth = revenueThisMonth,
                RevenueLastMonth = revenueLastMonth,
                GrowthRate = growthRate,
                AverageOrderValue = orders.Any() ? Math.Round(orders.Average(o => o.Total), 2) : 0,
                TotalOrders = orders.Count,
                MonthlyRevenue = monthlyRevenue,
                TopTenants = topTenants
            };

            return ApiResponse<RevenueReportDto>.Ok(result);
        }

        // ── Orders Report ─────────────────────────────────────────────────────
        public async Task<ApiResponse<OrdersReportDto>> GetOrdersReportAsync(ReportQueryParams query)
        {
            var allOrders = await _unitOfWork.Orders.GetAllAsync();
            var orders = allOrders.ToList();

            if (query.From.HasValue) orders = orders.Where(o => o.CreatedAt >= query.From).ToList();
            if (query.To.HasValue) orders = orders.Where(o => o.CreatedAt <= query.To).ToList();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);

            var thisMonth = orders.Count(o => o.CreatedAt >= startOfMonth);
            var lastMonth = orders.Count(o => o.CreatedAt >= startOfLastMonth && o.CreatedAt < startOfMonth);
            var growthRate = lastMonth > 0
                ? Math.Round((decimal)(thisMonth - lastMonth) / lastMonth * 100, 2)
                : 0;

            var total = orders.Count;
            var statusGroups = orders
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusBreakdownDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Percentage = total > 0 ? Math.Round((decimal)g.Count() / total * 100, 2) : 0
                })
                .ToList();

            // Monthly Orders آخر 12 شهر
            var monthlyOrders = new List<MonthlyCountDto>();
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                monthlyOrders.Add(new MonthlyCountDto
                {
                    Year = monthStart.Year,
                    Month = monthStart.Month,
                    MonthName = monthStart.ToString("MMM yyyy"),
                    Count = orders.Count(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd)
                });
            }

            var delivered = orders.Count(o => o.Status == OrderStatus.Delivered);
            var cancelled = orders.Count(o => o.Status == OrderStatus.Cancelled);

            var result = new OrdersReportDto
            {
                TotalOrders = total,
                OrdersThisMonth = thisMonth,
                OrdersLastMonth = lastMonth,
                GrowthRate = growthRate,
                PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
                ProcessingOrders = orders.Count(o => o.Status == OrderStatus.Processing),
                ShippedOrders = orders.Count(o => o.Status == OrderStatus.Shipped),
                DeliveredOrders = delivered,
                CancelledOrders = cancelled,
                ReturnedOrders = orders.Count(o => o.Status == OrderStatus.Returned),
                CancellationRate = total > 0 ? Math.Round((decimal)cancelled / total * 100, 2) : 0,
                DeliveryRate = total > 0 ? Math.Round((decimal)delivered / total * 100, 2) : 0,
                MonthlyOrders = monthlyOrders,
                StatusBreakdown = statusGroups
            };

            return ApiResponse<OrdersReportDto>.Ok(result);
        }

        // ── Subscriptions Report ──────────────────────────────────────────────
        public async Task<ApiResponse<SubscriptionsReportDto>> GetSubscriptionsReportAsync(ReportQueryParams query)
        {
            var allSubs = await _unitOfWork.Subscriptions.GetAllAsync();
            var subs = allSubs.ToList();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);

            var active = subs.Where(s => s.Status == SubscriptionStatus.Active).ToList();
            var expired = subs.Where(s => s.Status == SubscriptionStatus.Expired).ToList();
            var cancelled = subs.Where(s => s.Status == SubscriptionStatus.Cancelled).ToList();

            var newThisMonth = subs.Count(s => s.CreatedAt >= startOfMonth);
            var churnedThisMonth = subs.Count(s =>
                (s.Status == SubscriptionStatus.Cancelled || s.Status == SubscriptionStatus.Expired) &&
                s.UpdatedAt >= startOfMonth);

            var churnRate = active.Count > 0
                ? Math.Round((decimal)churnedThisMonth / (active.Count + churnedThisMonth) * 100, 2)
                : 0;

            // MRR
            var mrr = active
                .Where(s => s.Period == SubscriptionPeriod.Monthly)
                .Sum(s => s.Price)
                + active
                .Where(s => s.Period == SubscriptionPeriod.Yearly)
                .Sum(s => s.Price / 12);

            // Plan Breakdown
            var allPlans = await _unitOfWork.Plans.GetAllAsync();
            var planBreakdown = subs
                .Where(s => s.PlanId.HasValue)
                .GroupBy(s => s.PlanId!.Value)
                .Select(g =>
                {
                    var plan = allPlans.FirstOrDefault(p => p.Id == g.Key);
                    return new PlanBreakdownDto
                    {
                        PlanId = g.Key,
                        PlanName = plan?.Name ?? "—",
                        Count = g.Count(),
                        Percentage = subs.Count > 0
                            ? Math.Round((decimal)g.Count() / subs.Count * 100, 2) : 0,
                        Revenue = g.Sum(s => s.Price)
                    };
                })
                .OrderByDescending(p => p.Count)
                .ToList();

            // Monthly Subscriptions آخر 12 شهر
            var monthlySubs = new List<MonthlyCountDto>();
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                monthlySubs.Add(new MonthlyCountDto
                {
                    Year = monthStart.Year,
                    Month = monthStart.Month,
                    MonthName = monthStart.ToString("MMM yyyy"),
                    Count = subs.Count(s => s.CreatedAt >= monthStart && s.CreatedAt < monthEnd)
                });
            }

            // Expiring Soon — خلال 30 يوم
            var expiringSoon = active
                .Where(s => s.EndDate <= now.AddDays(30))
                .OrderBy(s => s.EndDate)
                .Take(20)
                .ToList();

            var expiringSoonDtos = new List<ExpiringSubscriptionDto>();
            foreach (var sub in expiringSoon)
            {
                var tenant = sub.TenantId.HasValue
                    ? await _unitOfWork.Tenants.GetByIdAsync(sub.TenantId.Value) : null;
                var plan = sub.PlanId.HasValue
                    ? await _unitOfWork.Plans.GetByIdAsync(sub.PlanId.Value) : null;

                expiringSoonDtos.Add(new ExpiringSubscriptionDto
                {
                    TenantId = sub.TenantId ?? Guid.Empty,
                    TenantName = tenant?.Name ?? "—",
                    PlanName = plan?.Name ?? "—",
                    EndDate = sub.EndDate,
                    DaysLeft = (sub.EndDate - now).Days
                });
            }

            var result = new SubscriptionsReportDto
            {
                TotalSubscriptions = subs.Count,
                ActiveSubscriptions = active.Count,
                ExpiredSubscriptions = expired.Count,
                CancelledSubscriptions = cancelled.Count,
                NewSubscriptionsThisMonth = newThisMonth,
                ChurnedThisMonth = churnedThisMonth,
                ChurnRate = churnRate,
                MonthlyRecurringRevenue = Math.Round(mrr, 2),
                AnnualRecurringRevenue = Math.Round(mrr * 12, 2),
                PlanBreakdown = planBreakdown,
                MonthlySubscriptions = monthlySubs,
                ExpiringSoon = expiringSoonDtos
            };

            return ApiResponse<SubscriptionsReportDto>.Ok(result);
        }
    }
}
