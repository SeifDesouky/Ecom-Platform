using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.TikTokShop
{
    /// <summary>
    /// يعالج WebhookEvent من TikTok Shop بعد ما يتحفظ في DB.
    /// TikTok بيبعت الـ type كـ integer في الـ payload نفسه:
    ///   1 = ORDER_STATUS_CHANGE
    ///   3 = PRODUCT_STATUS_CHANGE
    ///   4 = INVENTORY_UPDATE
    /// الـ payload بيكون متغلف بـ: { "type": 1, "shop_id": "...", "data": {...} }
    /// </summary>
    public sealed class TikTokShopWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TikTokShopWebhookProcessor> _logger;

        public TikTokShopWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<TikTokShopWebhookProcessor> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid webhookEventId, CancellationToken ct)
        {
            var webhookEvent = await _unitOfWork.WebhookEvents.GetByIdAsync(webhookEventId);
            if (webhookEvent == null) return;

            webhookEvent.Status = WebhookEventStatus.Processing;
            webhookEvent.LastAttemptAt = DateTime.UtcNow;
            webhookEvent.RetryCount++;
            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                await (webhookEvent.EventType switch
                {
                    "order.status_change" => HandleOrderStatusChangeAsync(webhookEvent, ct),
                    "product.status_change" => HandleProductStatusChangeAsync(webhookEvent, ct),
                    "inventory.update" => HandleInventoryUpdateAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[TikTokWebhook] Failed to process event {Id} — type {Type}",
                    webhookEventId, webhookEvent.EventType);

                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private async Task HandleOrderStatusChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;

            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);

            if (order == null)
            {
                _logger.LogWarning(
                    "[TikTokWebhook] order.status_change — Order {ExtId} not found.", externalId);
                return;
            }

            var newStatus = GetString(data, "order_status");
            order.Status = MapOrderStatus(newStatus);
            order.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[TikTokWebhook] order.status_change — Order {ExtId} → {Status}", externalId, newStatus);
        }

        private async Task HandleProductStatusChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;

            var externalId = GetString(data, "product_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[TikTokWebhook] product.status_change — Product {ExtId} not found.", externalId);
                return;
            }

            var status = GetString(data, "status");
            product.IsActive = status == "ACTIVATE";
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[TikTokWebhook] product.status_change — Product {ExtId} → {Status}", externalId, status);
        }

        private async Task HandleInventoryUpdateAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;

            var externalId = GetString(data, "product_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[TikTokWebhook] inventory.update — Product {ExtId} not found.", externalId);
                return;
            }

            if (data.TryGetProperty("available_stock", out var sq) &&
                sq.TryGetInt32(out var qty))
            {
                product.Stock = qty;
                product.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Products.UpdateAsync(product);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "[TikTokWebhook] inventory.update — Product {ExtId} stock → {Stock}", externalId, qty);
            }
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "[TikTokWebhook] Unknown event type: {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapOrderStatus(string status) =>
            status switch
            {
                "UNPAID" => OrderStatus.Pending,
                "AWAITING_SHIPMENT" => OrderStatus.Processing,
                "PARTIALLY_SHIPPING" => OrderStatus.Processing,
                "AWAITING_COLLECTION" => OrderStatus.Processing,
                "IN_TRANSIT" => OrderStatus.Shipped,
                "DELIVERED" => OrderStatus.Delivered,
                "COMPLETED" => OrderStatus.Delivered,
                "CANCELLED" => OrderStatus.Cancelled,
                _ => OrderStatus.Pending
            };

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}