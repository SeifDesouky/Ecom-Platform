using EcomPlatform.Application.DTOs.Notifications;

namespace EcomPlatform.Application.Services.Interfaces
{
    /// <summary>
    /// يُستخدم لإرسال الإشعارات في الوقت الفعلي عبر SignalR
    /// يُحقن في أي Service محتاج يبعت notification فوري
    /// </summary>
    public interface IRealtimeNotificationService
    {
        // إرسال لـ user معين
        Task SendToUserAsync(string userId, string eventName, object data);

        // إرسال لكل users الـ tenant
        Task SendToTenantAsync(string tenantId, string eventName, object data);

        // إرسال notification كاملة لـ user
        Task SendNotificationAsync(string userId, NotificationResponseDto notification);

        // إرسال notification لـ tenant كله (مثلاً low stock alert)
        Task SendTenantNotificationAsync(string tenantId, NotificationResponseDto notification);

        // أحداث محددة
        Task SendOrderStatusChangedAsync(string tenantId, Guid orderId, string orderNumber, string newStatus);
        Task SendLowStockAlertAsync(string tenantId, Guid productId, string productName, int currentStock);
        Task SendNewOrderAsync(string tenantId, Guid orderId, string orderNumber, decimal total);
        Task SendPaymentReceivedAsync(string tenantId, Guid orderId, decimal amount);
        Task SendReturnRequestAsync(string tenantId, Guid returnId, string returnNumber);
    }
}
