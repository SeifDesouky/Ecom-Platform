using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Notifications;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface INotificationService
    {
        Task<ApiResponse<NotificationResponseDto>> CreateAsync(CreateNotificationDto dto);
        Task<ApiResponse<NotificationStatsDto>> GetByUserAsync(Guid userId);
        Task<ApiResponse<bool>> MarkAsReadAsync(Guid id);
        Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId);
        Task<ApiResponse<int>> GetUnreadCountAsync(Guid userId);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
    }
}