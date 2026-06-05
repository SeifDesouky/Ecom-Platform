using EcomPlatform.Application.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EcomPlatform.API.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        // كل user بيتضاف لـ group باسمه عشان نبعت له notifications مخصصة
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? Context.User?.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            var tenantId = Context.User?.FindFirst("tenantId")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? Context.User?.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(userId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

            await base.OnDisconnectedAsync(exception);
        }

        // Client يستطيع يطلب subscribe لـ tenant معين (للـ Admin)
        public async Task JoinTenantGroup(string tenantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }

        public async Task LeaveTenantGroup(string tenantId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }
    }
}
