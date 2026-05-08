using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Plans;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class PlanService : IPlanService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PlanService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<PlanResponseDto>> CreateAsync(CreatePlanDto dto)
        {
            var plan = new Plan
            {
                Name = dto.Name,
                Description = dto.Description,
                MonthlyPrice = dto.MonthlyPrice,
                YearlyPrice = dto.YearlyPrice,
                IsPopular = dto.IsPopular,
                MaxProducts = dto.MaxProducts,
                MaxOrders = dto.MaxOrders,
                MaxCustomers = dto.MaxCustomers,
                MaxUsers = dto.MaxUsers,
                HasAnalytics = dto.HasAnalytics,
                HasAPI = dto.HasAPI,
                HasMultiCurrency = dto.HasMultiCurrency,
                HasCustomDomain = dto.HasCustomDomain,
                HasPrioritySupport = dto.HasPrioritySupport,
                IsActive = true
            };

            await _unitOfWork.Plans.AddAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<PlanResponseDto>.Ok(
                MapToDto(plan),
                "Plan created successfully");
        }

        public async Task<ApiResponse<PlanResponseDto>> GetByIdAsync(Guid id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                return ApiResponse<PlanResponseDto>.Fail("Plan not found");

            return ApiResponse<PlanResponseDto>.Ok(MapToDto(plan));
        }

        public async Task<ApiResponse<IEnumerable<PlanResponseDto>>> GetAllAsync()
        {
            var plans = await _unitOfWork.Plans.GetAllAsync();

            return ApiResponse<IEnumerable<PlanResponseDto>>
                .Ok(plans.Select(MapToDto));
        }

        public async Task<ApiResponse<PlanResponseDto>> UpdateAsync(
            Guid id,
            CreatePlanDto dto)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                return ApiResponse<PlanResponseDto>.Fail("Plan not found");

            plan.Name = dto.Name;
            plan.Description = dto.Description;
            plan.MonthlyPrice = dto.MonthlyPrice;
            plan.YearlyPrice = dto.YearlyPrice;
            plan.IsPopular = dto.IsPopular;
            plan.MaxProducts = dto.MaxProducts;
            plan.MaxOrders = dto.MaxOrders;
            plan.MaxCustomers = dto.MaxCustomers;
            plan.MaxUsers = dto.MaxUsers;
            plan.HasAnalytics = dto.HasAnalytics;
            plan.HasAPI = dto.HasAPI;
            plan.HasMultiCurrency = dto.HasMultiCurrency;
            plan.HasCustomDomain = dto.HasCustomDomain;
            plan.HasPrioritySupport = dto.HasPrioritySupport;

            await _unitOfWork.Plans.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<PlanResponseDto>.Ok(
                MapToDto(plan),
                "Plan updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                return ApiResponse<bool>.Fail("Plan not found");

            await _unitOfWork.Plans.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(
                true,
                "Plan deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                return ApiResponse<bool>.Fail("Plan not found");

            plan.IsActive = !plan.IsActive;

            await _unitOfWork.Plans.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            var message = plan.IsActive
                ? "Plan activated"
                : "Plan deactivated";

            return ApiResponse<bool>.Ok(true, message);
        }

        public async Task<ApiResponse<SubscriptionResponseDto>> SubscribeAsync(
            CreateSubscriptionDto dto)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(dto.PlanId);

            if (plan == null)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Plan not found");

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(dto.TenantId);

            if (tenant == null)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Tenant not found");

            var existingSubs = await _unitOfWork.Subscriptions.FindAsync(s =>
                s.TenantId == dto.TenantId &&
                s.Status == SubscriptionStatus.Active);

            foreach (var sub in existingSubs)
            {
                sub.Status = SubscriptionStatus.Cancelled;
                sub.CancelledAt = DateTime.UtcNow;
                await _unitOfWork.Subscriptions.UpdateAsync(sub);
            }

            var price = dto.Period == SubscriptionPeriod.Monthly
                ? plan.MonthlyPrice
                : plan.YearlyPrice;

            var startDate = DateTime.UtcNow;

            var endDate = dto.Period == SubscriptionPeriod.Monthly
                ? startDate.AddMonths(1)
                : startDate.AddYears(1);

            var subscription = new Subscription
            {
                TenantId = dto.TenantId,
                PlanId = dto.PlanId,
                Status = SubscriptionStatus.Active,
                Period = dto.Period,
                Price = price,
                StartDate = startDate,
                EndDate = endDate,
                AutoRenew = dto.AutoRenew,
                Notes = dto.Notes
            };

            tenant.SubscriptionEndDate = endDate;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.Subscriptions.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            subscription.Plan = plan;
            subscription.Tenant = tenant;

            return ApiResponse<SubscriptionResponseDto>.Ok(
                MapSubscriptionToDto(subscription),
                "Subscribed successfully");
        }

        public async Task<ApiResponse<SubscriptionResponseDto>>
            GetTenantSubscriptionAsync(Guid tenantId)
        {
            var subs = await _unitOfWork.Subscriptions.FindAsync(s =>
                s.TenantId == tenantId &&
                s.Status == SubscriptionStatus.Active);

            var subscription = subs.FirstOrDefault();

            if (subscription == null)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("No active subscription found");

            if (!subscription.PlanId.HasValue)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Subscription has no associated plan");

            var plan = await _unitOfWork.Plans
                .GetByIdAsync(subscription.PlanId.Value);

            var tenant = await _unitOfWork.Tenants
                .GetByIdAsync(tenantId);

            subscription.Plan = plan;
            subscription.Tenant = tenant;

            return ApiResponse<SubscriptionResponseDto>
                .Ok(MapSubscriptionToDto(subscription));
        }

        public async Task<ApiResponse<bool>> CancelSubscriptionAsync(
            Guid subscriptionId)
        {
            var subscription = await _unitOfWork.Subscriptions
                .GetByIdAsync(subscriptionId);

            if (subscription == null)
                return ApiResponse<bool>.Fail("Subscription not found");

            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledAt = DateTime.UtcNow;
            subscription.AutoRenew = false;

            await _unitOfWork.Subscriptions.UpdateAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Subscription cancelled successfully");
        }

        public async Task<ApiResponse<SubscriptionResponseDto>>
            RenewSubscriptionAsync(Guid subscriptionId)
        {
            var subscription = await _unitOfWork.Subscriptions
                .GetByIdAsync(subscriptionId);

            if (subscription == null)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Subscription not found");

            if (!subscription.PlanId.HasValue)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Subscription has no associated plan");

            var plan = await _unitOfWork.Plans
                .GetByIdAsync(subscription.PlanId.Value);

            if (plan == null)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Plan not found");

            subscription.StartDate = DateTime.UtcNow;
            subscription.EndDate = subscription.Period == SubscriptionPeriod.Monthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1);
            subscription.Status = SubscriptionStatus.Active;

            if (!subscription.TenantId.HasValue)
                return ApiResponse<SubscriptionResponseDto>
                    .Fail("Subscription has no associated tenant");

            var tenant = await _unitOfWork.Tenants
                .GetByIdAsync(subscription.TenantId.Value);

            if (tenant != null)
            {
                tenant.SubscriptionEndDate = subscription.EndDate;
                await _unitOfWork.Tenants.UpdateAsync(tenant);
            }

            await _unitOfWork.Subscriptions.UpdateAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            subscription.Plan = plan;
            subscription.Tenant = tenant;

            return ApiResponse<SubscriptionResponseDto>.Ok(
                MapSubscriptionToDto(subscription),
                "Subscription renewed successfully");
        }

        private static PlanResponseDto MapToDto(Plan plan) => new()
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            MonthlyPrice = plan.MonthlyPrice,
            YearlyPrice = plan.YearlyPrice,
            IsActive = plan.IsActive,
            IsPopular = plan.IsPopular,
            MaxProducts = plan.MaxProducts,
            MaxOrders = plan.MaxOrders,
            MaxCustomers = plan.MaxCustomers,
            MaxUsers = plan.MaxUsers,
            HasAnalytics = plan.HasAnalytics,
            HasAPI = plan.HasAPI,
            HasMultiCurrency = plan.HasMultiCurrency,
            HasCustomDomain = plan.HasCustomDomain,
            HasPrioritySupport = plan.HasPrioritySupport,
            CreatedAt = plan.CreatedAt
        };

        private static SubscriptionResponseDto MapSubscriptionToDto(
            Subscription subscription) => new()
            {
                Id = subscription.Id,
                // ✅ FIX: استخدام .GetValueOrDefault() لتحويل Guid? إلى Guid
                TenantId = subscription.TenantId.GetValueOrDefault(),
                TenantName = subscription.Tenant?.Name ?? string.Empty,
                PlanId = subscription.PlanId.GetValueOrDefault(),
                PlanName = subscription.Plan?.Name ?? string.Empty,
                Status = subscription.Status,
                Period = subscription.Period,
                Price = subscription.Price,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                AutoRenew = subscription.AutoRenew,
                CancelledAt = subscription.CancelledAt,
                Notes = subscription.Notes,
                CreatedAt = subscription.CreatedAt
            };
    }
}