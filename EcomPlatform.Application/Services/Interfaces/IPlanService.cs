using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Plans;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IPlanService
    {
        Task<ApiResponse<PlanResponseDto>> CreateAsync(CreatePlanDto dto);
        Task<ApiResponse<PlanResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<PlanResponseDto>>> GetAllAsync();
        Task<ApiResponse<PlanResponseDto>> UpdateAsync(Guid id, CreatePlanDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
        Task<ApiResponse<SubscriptionResponseDto>> SubscribeAsync(CreateSubscriptionDto dto);
        Task<ApiResponse<SubscriptionResponseDto>> GetTenantSubscriptionAsync(Guid tenantId);
        Task<ApiResponse<bool>> CancelSubscriptionAsync(Guid subscriptionId);
        Task<ApiResponse<SubscriptionResponseDto>> RenewSubscriptionAsync(Guid subscriptionId);
        Task<ApiResponse<IEnumerable<SubscriptionResponseDto>>> GetAllSubscriptionsAsync(int page, int limit);

    }
}