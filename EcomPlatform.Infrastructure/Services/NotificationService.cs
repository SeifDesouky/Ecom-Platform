using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Notifications;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRealtimeNotificationService _realtime;

        public NotificationService(
            IUnitOfWork unitOfWork,
            IRealtimeNotificationService realtime)
        {
            _unitOfWork = unitOfWork;
            _realtime = realtime;
        }

        public async Task<ApiResponse<NotificationResponseDto>> CreateAsync(CreateNotificationDto dto)
        {
            var notification = new Notification
            {
                Title = dto.Title,
                Message = dto.Message,
                Type = dto.Type,
                ActionUrl = dto.ActionUrl,
                Icon = dto.Icon,
                UserId = dto.UserId,
                TenantId = dto.TenantId,
                IsRead = false
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            var dto_result = MapToDto(notification);

            // بعت الـ notification فوراً عبر SignalR
            _ = _realtime.SendNotificationAsync(dto.UserId.ToString(), dto_result);

            return ApiResponse<NotificationResponseDto>.Ok(dto_result, "Notification created successfully");
        }

        public async Task<ApiResponse<NotificationStatsDto>> GetByUserAsync(Guid userId)
        {
            var notifications = await _unitOfWork.Notifications.FindAsync(n => n.UserId == userId);
            var list = notifications.OrderByDescending(n => n.CreatedAt).ToList();

            return ApiResponse<NotificationStatsDto>.Ok(new NotificationStatsDto
            {
                TotalCount = list.Count,
                UnreadCount = list.Count(n => !n.IsRead),
                Notifications = list.Select(MapToDto).ToList()
            });
        }

        public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid id)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (notification == null)
                return ApiResponse<bool>.Fail("Notification not found");

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _unitOfWork.Notifications.UpdateAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Notification marked as read");
        }

        public async Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId)
        {
            var notifications = await _unitOfWork.Notifications.FindAsync(n =>
                n.UserId == userId && !n.IsRead);

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _unitOfWork.Notifications.UpdateAsync(notification);
            }

            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "All notifications marked as read");
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(Guid userId)
        {
            var notifications = await _unitOfWork.Notifications.FindAsync(n =>
                n.UserId == userId && !n.IsRead);

            return ApiResponse<int>.Ok(notifications.Count());
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (notification == null)
                return ApiResponse<bool>.Fail("Notification not found");

            await _unitOfWork.Notifications.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Notification deleted successfully");
        }

        private static NotificationResponseDto MapToDto(Notification notification) => new()
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            ActionUrl = notification.ActionUrl,
            Icon = notification.Icon,
            UserId = notification.UserId,
            TenantId = notification.TenantId,
            CreatedAt = notification.CreatedAt
        };
    }
}