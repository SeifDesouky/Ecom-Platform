using EcomPlatform.Application.DTOs.Notifications;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EcomPlatform.Infrastructure.Services
{
    public class RealtimeNotificationService : IRealtimeNotificationService
    {
        private readonly IHubContext<Hub> _hubContext;

        public RealtimeNotificationService(IHubContext<Hub> hubContext)
        {
            _hubContext = hubContext;
        }

        // ── Generic ───────────────────────────────────────────────────────────

        public async Task SendToUserAsync(string userId, string eventName, object data)
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync(eventName, data);
        }

        public async Task SendToTenantAsync(string tenantId, string eventName, object data)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync(eventName, data);
        }

        // ── Notification DTOs ─────────────────────────────────────────────────

        public async Task SendNotificationAsync(string userId, NotificationResponseDto notification)
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notification);
        }

        public async Task SendTenantNotificationAsync(string tenantId, NotificationResponseDto notification)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync("ReceiveNotification", notification);
        }

        // ── Business Events ───────────────────────────────────────────────────

        public async Task SendOrderStatusChangedAsync(
            string tenantId, Guid orderId, string orderNumber, string newStatus)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync("OrderStatusChanged", new
                {
                    orderId,
                    orderNumber,
                    newStatus,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task SendLowStockAlertAsync(
            string tenantId, Guid productId, string productName, int currentStock)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync("LowStockAlert", new
                {
                    productId,
                    productName,
                    currentStock,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task SendNewOrderAsync(
            string tenantId, Guid orderId, string orderNumber, decimal total)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync("NewOrder", new
                {
                    orderId,
                    orderNumber,
                    total,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task SendPaymentReceivedAsync(string tenantId, Guid orderId, decimal amount)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync("PaymentReceived", new
                {
                    orderId,
                    amount,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task SendReturnRequestAsync(string tenantId, Guid returnId, string returnNumber)
        {
            await _hubContext.Clients
                .Group($"tenant_{tenantId}")
                .SendAsync("NewReturnRequest", new
                {
                    returnId,
                    returnNumber,
                    timestamp = DateTime.UtcNow
                });
        }
    }
}