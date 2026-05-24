using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.Amazon
{
    /// <summary>
    /// يعالج WebhookEvent من Amazon SP-API بعد ما يتحفظ في DB.
    /// Amazon بيبعت الـ notifications عن طريق AWS SNS.
    /// الـ payload بيكون: { "NotificationType": "...", "Payload": { ... } }
    /// NotificationTypes المهمة:
    ///   ORDER_CHANGE, ITEM_INVENTORY_EVENT_DATA, LISTINGS_ITEM_STATUS_CHANGE
    /// </summary>
    public sealed class AmazonWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AmazonWebhookProcessor> _logger;

        public AmazonWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<AmazonWebhookProcessor> logger)
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
                    "ORDER_CHANGE" => HandleOrderChangeAsync(webhookEvent, ct),
                    "ITEM_INVENTORY_EVENT_DATA" => HandleInventoryEventAsync(webhookEvent, ct),
                    "LISTINGS_ITEM_STATUS_CHANGE" => HandleListingStatusChangeAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[AmazonWebhook] Failed to process event {Id} — type {Type}",
                    webhookEventId, webhookEvent.EventType);

                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private async Task HandleOrderChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var root = payload.RootElement;

            // Amazon SNS: الـ Payload بيكون string JSON داخل string
            var orderPayload = ExtractPayload(root, "OrderChangeNotification");
            if (orderPayload.ValueKind == JsonValueKind.Undefined)
            {
                _logger.LogWarning("[AmazonWebhook] ORDER_CHANGE — could not extract OrderChangeNotification.");
                return;
            }

            var externalId = GetString(orderPayload, "AmazonOrderId");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);

            var orderStatus = GetString(orderPayload, "OrderStatus");

            if (order == null)
            {
                order = new Order
                {
                    Id = Guid.NewGuid(),
                    ExternalId = externalId,
                    StoreIntegrationId = e.StoreIntegrationId,
                    ExternalOrderNumber = externalId,
                    OrderNumber = externalId,
                    Status = MapOrderStatus(orderStatus),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                if (orderPayload.TryGetProperty("BuyerInfo", out var buyer))
                {
                    order.CustomerEmail = GetString(buyer, "BuyerEmail");
                    order.CustomerName = GetString(buyer, "BuyerName");
                }

                await _unitOfWork.Orders.AddAsync(order);
                _logger.LogInformation(
                    "[AmazonWebhook] ORDER_CHANGE — Inserted Order {ExtId}", externalId);
            }
            else
            {
                order.Status = MapOrderStatus(orderStatus);
                order.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Orders.UpdateAsync(order);
                _logger.LogInformation(
                    "[AmazonWebhook] ORDER_CHANGE — Updated Order {ExtId} → {Status}", externalId, orderStatus);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleInventoryEventAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var root = payload.RootElement;

            var inventoryPayload = ExtractPayload(root, "InventoryEventData");
            if (inventoryPayload.ValueKind == JsonValueKind.Undefined) return;

            // Amazon بيستخدم ASIN أو FNSKU كـ external ID
            var externalId = GetString(inventoryPayload, "ASIN");
            if (string.IsNullOrEmpty(externalId))
                externalId = GetString(inventoryPayload, "FNSKU");

            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[AmazonWebhook] ITEM_INVENTORY_EVENT_DATA — Product {ExtId} not found.", externalId);
                return;
            }

            if (inventoryPayload.TryGetProperty("Quantity", out var qEl) &&
                qEl.TryGetInt32(out var qty))
            {
                product.Stock = qty;
                product.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Products.UpdateAsync(product);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "[AmazonWebhook] ITEM_INVENTORY_EVENT_DATA — Product {ExtId} stock → {Stock}", externalId, qty);
            }
        }

        private async Task HandleListingStatusChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var root = payload.RootElement;

            var listingPayload = ExtractPayload(root, "ListingsItemStatusChangeNotification");
            if (listingPayload.ValueKind == JsonValueKind.Undefined) return;

            var externalId = GetString(listingPayload, "Asin");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[AmazonWebhook] LISTINGS_ITEM_STATUS_CHANGE — Product {ExtId} not found.", externalId);
                return;
            }

            var status = GetString(listingPayload, "Status");
            product.IsActive = status == "BUYABLE";
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[AmazonWebhook] LISTINGS_ITEM_STATUS_CHANGE — Product {ExtId} → {Status}", externalId, status);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "[AmazonWebhook] Unknown notification type: {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // ── Amazon SNS payload extractor ──────────────────────────────────────
        // Amazon بيبعت الـ payload كـ nested JSON string داخل "Payload" property
        private static JsonElement ExtractPayload(JsonElement root, string key)
        {
            try
            {
                if (root.TryGetProperty("Payload", out var payloadStr))
                {
                    var inner = payloadStr.GetString() ?? "{}";
                    using var doc = JsonDocument.Parse(inner);
                    if (doc.RootElement.TryGetProperty(key, out var result))
                    {
                        // لازم نعمل clone عشان الـ doc هيتdispose
                        return result.Clone();
                    }
                }
                // fallback: لو الـ payload مش متغلف
                if (root.TryGetProperty(key, out var direct))
                    return direct;
            }
            catch { /* malformed payload */ }
            return default;
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapOrderStatus(string status) =>
            status switch
            {
                "Pending" => OrderStatus.Pending,
                "Unshipped" => OrderStatus.Processing,
                "PartiallyShipped" => OrderStatus.Shipped,
                "Shipped" => OrderStatus.Shipped,
                "Delivered" => OrderStatus.Delivered,
                "Canceled" => OrderStatus.Cancelled,
                "Unfulfillable" => OrderStatus.Cancelled,
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