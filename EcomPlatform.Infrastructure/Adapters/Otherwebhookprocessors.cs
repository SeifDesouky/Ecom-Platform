// ══════════════════════════════════════════════════════════════════════════════
// EbayWebhookProcessor.cs
// ══════════════════════════════════════════════════════════════════════════════
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.eBay
{
    /// <summary>
    /// يعالج WebhookEvent من eBay بعد ما يتحفظ في DB.
    /// eBay بيبعت notifications عن طريق Notification API.
    /// الـ payload بيكون: { "metadata": { "topic": "..." }, "notification": { "data": {...} } }
    /// </summary>
    public sealed class EbayWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EbayWebhookProcessor> _logger;

        public EbayWebhookProcessor(IUnitOfWork unitOfWork, ILogger<EbayWebhookProcessor> logger)
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
                    "MARKETPLACE_ACCOUNT_DELETION" => HandleUnknownAsync(webhookEvent, ct),
                    "ITEM_SOLD" => HandleItemSoldAsync(webhookEvent, ct),
                    "ORDER_PAYMENT_COMPLETED" => HandleOrderPaymentAsync(webhookEvent, ct),
                    "ORDER_SHIPPED" => HandleOrderShippedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EbayWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleItemSoldAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = ExtractData(payload.RootElement);
            var externalId = GetString(data, "orderId");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) return;

            var order = new Order
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                StoreIntegrationId = e.StoreIntegrationId,
                ExternalOrderNumber = externalId,
                OrderNumber = externalId,
                Status = OrderStatus.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[EbayWebhook] ITEM_SOLD — Inserted Order {ExtId}", externalId);
        }

        private async Task HandleOrderPaymentAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = ExtractData(payload.RootElement);
            var externalId = GetString(data, "orderId");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) { _logger.LogWarning("[EbayWebhook] ORDER_PAYMENT — Order {ExtId} not found.", externalId); return; }

            order.Status = OrderStatus.Processing;
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[EbayWebhook] ORDER_PAYMENT_COMPLETED — Order {ExtId} → Processing", externalId);
        }

        private async Task HandleOrderShippedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = ExtractData(payload.RootElement);
            var externalId = GetString(data, "orderId");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) { _logger.LogWarning("[EbayWebhook] ORDER_SHIPPED — Order {ExtId} not found.", externalId); return; }

            order.Status = OrderStatus.Shipped;
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[EbayWebhook] ORDER_SHIPPED — Order {ExtId} → Shipped", externalId);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[EbayWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        private static JsonElement ExtractData(JsonElement root) =>
            root.TryGetProperty("notification", out var n) &&
            n.TryGetProperty("data", out var d) ? d : root;

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// NoonWebhookProcessor.cs
// ══════════════════════════════════════════════════════════════════════════════
namespace EcomPlatform.Infrastructure.Adapters.Noon
{
    /// <summary>
    /// يعالج WebhookEvent من Noon بعد ما يتحفظ في DB.
    /// Noon بيبعت: { "event_type": "...", "data": { ... } }
    /// </summary>
    public sealed class NoonWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<NoonWebhookProcessor> _logger;

        public NoonWebhookProcessor(IUnitOfWork unitOfWork, ILogger<NoonWebhookProcessor> logger)
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
                    "order.created" => HandleOrderCreatedAsync(webhookEvent, ct),
                    "order.status_change" => HandleOrderStatusChangeAsync(webhookEvent, ct),
                    "product.updated" => HandleProductUpdatedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NoonWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleOrderCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) return;

            var order = new Order
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                StoreIntegrationId = e.StoreIntegrationId,
                ExternalOrderNumber = externalId,
                OrderNumber = externalId,
                Status = OrderStatus.Pending,
                Total = GetDecimal(data, "total"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[NoonWebhook] order.created — Inserted Order {ExtId}", externalId);
        }

        private async Task HandleOrderStatusChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) { _logger.LogWarning("[NoonWebhook] order.status_change — Order {ExtId} not found.", externalId); return; }

            var status = GetString(data, "status");
            order.Status = MapOrderStatus(status);
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[NoonWebhook] order.status_change — Order {ExtId} → {Status}", externalId, status);
        }

        private async Task HandleProductUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "sku");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null) { _logger.LogWarning("[NoonWebhook] product.updated — Product {ExtId} not found.", externalId); return; }

            product.Price = GetDecimal(data, "price");
            if (data.TryGetProperty("quantity", out var qEl) && qEl.TryGetInt32(out var qty))
                product.Stock = qty;
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[NoonWebhook] product.updated — Product {ExtId} updated.", externalId);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[NoonWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        private static OrderStatus MapOrderStatus(string status) =>
            status switch
            {
                "created" or "pending_payment" => OrderStatus.Pending,
                "processing" => OrderStatus.Processing,
                "shipped" => OrderStatus.Shipped,
                "delivered" or "completed" => OrderStatus.Delivered,
                "cancelled" => OrderStatus.Cancelled,
                "returned" => OrderStatus.Returned,
                _ => OrderStatus.Pending
            };

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static decimal GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0m;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// NoonExpressWebhookProcessor.cs  — نفس pattern الـ NoonWebhookProcessor
// ══════════════════════════════════════════════════════════════════════════════
namespace EcomPlatform.Infrastructure.Adapters.NoonExpress
{
    /// <summary>
    /// يعالج WebhookEvent من Noon Express (Fulfillment by Noon).
    /// نفس structure الـ Noon مع events مختلفة تخص الـ fulfillment.
    /// </summary>
    public sealed class NoonExpressWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<NoonExpressWebhookProcessor> _logger;

        public NoonExpressWebhookProcessor(IUnitOfWork unitOfWork, ILogger<NoonExpressWebhookProcessor> logger)
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
                    "shipment.created" => HandleShipmentCreatedAsync(webhookEvent, ct),
                    "shipment.delivered" => HandleShipmentDeliveredAsync(webhookEvent, ct),
                    "order.status_change" => HandleOrderStatusChangeAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NoonExpressWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleShipmentCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;

            order.Status = OrderStatus.Shipped;
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[NoonExpressWebhook] shipment.created — Order {ExtId} → Shipped", externalId);
        }

        private async Task HandleShipmentDeliveredAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;

            order.Status = OrderStatus.Delivered;
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[NoonExpressWebhook] shipment.delivered — Order {ExtId} → Delivered", externalId);
        }

        private async Task HandleOrderStatusChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            var status = GetString(data, "status");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;

            order.Status = status switch
            {
                "cancelled" => OrderStatus.Cancelled,
                "returned" => OrderStatus.Returned,
                _ => order.Status
            };
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[NoonExpressWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// AliExpressWebhookProcessor.cs
// ══════════════════════════════════════════════════════════════════════════════
namespace EcomPlatform.Infrastructure.Adapters.AliExpress
{
    /// <summary>
    /// يعالج WebhookEvent من AliExpress بعد ما يتحفظ في DB.
    /// AliExpress بيبعت: { "topic": "...", "msg": "{...}" }  — الـ msg بيكون JSON string
    /// </summary>
    public sealed class AliExpressWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AliExpressWebhookProcessor> _logger;

        public AliExpressWebhookProcessor(IUnitOfWork unitOfWork, ILogger<AliExpressWebhookProcessor> logger)
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
                    "ORDER_PAID" or "order.paid" => HandleOrderPaidAsync(webhookEvent, ct),
                    "ORDER_SELLER_SHIP" or "order.shipped" => HandleOrderShippedAsync(webhookEvent, ct),
                    "ORDER_CLOSE" or "order.closed" => HandleOrderClosedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AliExpressWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleOrderPaidAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = ExtractMsg(payload.RootElement);
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) { existing.Status = OrderStatus.Processing; existing.UpdatedAt = DateTime.UtcNow; await _unitOfWork.Orders.UpdateAsync(existing); await _unitOfWork.SaveChangesAsync(); return; }

            var order = new Order { Id = Guid.NewGuid(), ExternalId = externalId, StoreIntegrationId = e.StoreIntegrationId, ExternalOrderNumber = externalId, OrderNumber = externalId, Status = OrderStatus.Processing, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[AliExpressWebhook] ORDER_PAID — Order {ExtId}", externalId);
        }

        private async Task HandleOrderShippedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = ExtractMsg(payload.RootElement);
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;
            order.Status = OrderStatus.Shipped; order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order); await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[AliExpressWebhook] ORDER_SELLER_SHIP — Order {ExtId} → Shipped", externalId);
        }

        private async Task HandleOrderClosedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = ExtractMsg(payload.RootElement);
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;
            order.Status = OrderStatus.Cancelled; order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order); await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[AliExpressWebhook] ORDER_CLOSE — Order {ExtId} → Cancelled", externalId);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[AliExpressWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // AliExpress بيبعت الـ msg كـ JSON string داخل الـ payload
        private static JsonElement ExtractMsg(JsonElement root)
        {
            if (root.TryGetProperty("msg", out var msgEl))
            {
                var msgStr = msgEl.GetString() ?? "{}";
                try
                {
                    using var doc = JsonDocument.Parse(msgStr);
                    return doc.RootElement.Clone();
                }
                catch { }
            }
            return root;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// GoogleShoppingWebhookProcessor.cs
// ══════════════════════════════════════════════════════════════════════════════
namespace EcomPlatform.Infrastructure.Adapters.GoogleShopping
{
    /// <summary>
    /// يعالج WebhookEvent من Google Shopping (Merchant Center) بعد ما يتحفظ في DB.
    /// Google بيبعت الـ notifications عن طريق Pub/Sub.
    /// الـ payload بيكون: { "message": { "data": "<base64>", "messageId": "..." } }
    /// الـ data بعد decode بيكون: { "resource_id": "...", "resource": "..." }
    /// </summary>
    public sealed class GoogleShoppingWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GoogleShoppingWebhookProcessor> _logger;

        public GoogleShoppingWebhookProcessor(IUnitOfWork unitOfWork, ILogger<GoogleShoppingWebhookProcessor> logger)
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
                    "product.status_change" => HandleProductStatusChangeAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoogleShoppingWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleProductStatusChangeAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = DecodeGooglePubSub(payload.RootElement);

            var externalId = GetString(data, "resource_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null) { _logger.LogWarning("[GoogleShoppingWebhook] product.status_change — Product {ExtId} not found.", externalId); return; }

            var status = GetString(data, "status");
            product.IsActive = status == "approved";
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[GoogleShoppingWebhook] product.status_change — Product {ExtId} → {Status}", externalId, status);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[GoogleShoppingWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // Google Pub/Sub بيبعت الـ data كـ base64
        private static JsonElement DecodeGooglePubSub(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("data", out var dataEl))
                {
                    var base64 = dataEl.GetString() ?? string.Empty;
                    var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.Clone();
                }
            }
            catch { }
            return root;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// YouCanWebhookProcessor.cs
// ══════════════════════════════════════════════════════════════════════════════
namespace EcomPlatform.Infrastructure.Adapters.YouCan
{
    /// <summary>
    /// يعالج WebhookEvent من YouCan بعد ما يتحفظ في DB.
    /// YouCan بيبعت نفس structure الـ Zid تقريباً:
    ///   { "event": "...", "data": { ... } }
    /// </summary>
    public sealed class YouCanWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<YouCanWebhookProcessor> _logger;

        public YouCanWebhookProcessor(IUnitOfWork unitOfWork, ILogger<YouCanWebhookProcessor> logger)
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
                    "order.created" => HandleOrderCreatedAsync(webhookEvent, ct),
                    "order.updated" => HandleOrderUpdatedAsync(webhookEvent, ct),
                    "product.created" => HandleProductCreatedAsync(webhookEvent, ct),
                    "product.updated" => HandleProductUpdatedAsync(webhookEvent, ct),
                    "product.deleted" => HandleProductDeletedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[YouCanWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleOrderCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) return;

            var order = new Order { Id = Guid.NewGuid(), ExternalId = externalId, StoreIntegrationId = e.StoreIntegrationId, ExternalOrderNumber = GetString(data, "reference"), OrderNumber = GetString(data, "reference"), Status = MapOrderStatus(GetString(data, "status")), Total = GetDecimal(data, "total"), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

            if (data.TryGetProperty("customer", out var customer))
            { order.CustomerName = GetString(customer, "fullName"); order.CustomerEmail = GetString(customer, "email"); order.CustomerPhone = GetString(customer, "phone"); }

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[YouCanWebhook] order.created — Inserted Order {ExtId}", externalId);
        }

        private async Task HandleOrderUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;
            order.Status = MapOrderStatus(GetString(data, "status"));
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleProductCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) return;

            var product = new Product { Id = Guid.NewGuid(), ExternalId = externalId, StoreIntegrationId = e.StoreIntegrationId, Name = GetString(data, "name"), SKU = GetString(data, "sku"), Price = GetDecimal(data, "price"), IsActive = true, Status = ProductStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[YouCanWebhook] product.created — Inserted Product {ExtId}", externalId);
        }

        private async Task HandleProductUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null) return;
            product.Name = GetString(data, "name"); product.Price = GetDecimal(data, "price"); product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product); await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleProductDeletedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null) return;
            product.IsActive = false; product.Status = ProductStatus.Deleted; product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product); await _unitOfWork.SaveChangesAsync();
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[YouCanWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        private static OrderStatus MapOrderStatus(string status) =>
            status switch { "pending" => OrderStatus.Pending, "confirmed" => OrderStatus.Processing, "shipped" => OrderStatus.Shipped, "delivered" => OrderStatus.Delivered, "cancelled" => OrderStatus.Cancelled, _ => OrderStatus.Pending };

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static decimal GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0m;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// ExpandCartWebhookProcessor.cs  — يشمل ExpandCart Gulf + ExpandCart Egypt
// ══════════════════════════════════════════════════════════════════════════════
namespace EcomPlatform.Infrastructure.Adapters.ExpandCart
{
    /// <summary>
    /// يعالج WebhookEvent من ExpandCart (Gulf + Egypt) بعد ما يتحفظ في DB.
    /// ExpandCart بيبعت نفس structure الـ OpenCart:
    ///   { "event": "...", "data": { ... } }
    /// نفس الـ Processor بيشتغل لـ ExpandCart Gulf و ExpandCart Egypt.
    /// </summary>
    public sealed class ExpandCartWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExpandCartWebhookProcessor> _logger;

        public ExpandCartWebhookProcessor(IUnitOfWork unitOfWork, ILogger<ExpandCartWebhookProcessor> logger)
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
                    "order.add" or "order.created" => HandleOrderCreatedAsync(webhookEvent, ct),
                    "order.edit" or "order.updated" => HandleOrderUpdatedAsync(webhookEvent, ct),
                    "product.add" or "product.created" => HandleProductCreatedAsync(webhookEvent, ct),
                    "product.edit" or "product.updated" => HandleProductUpdatedAsync(webhookEvent, ct),
                    "product.delete" or "product.deleted" => HandleProductDeletedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpandCartWebhook] Failed {Id} — {Type}", webhookEventId, webhookEvent.EventType);
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleOrderCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) return;

            var order = new Order { Id = Guid.NewGuid(), ExternalId = externalId, StoreIntegrationId = e.StoreIntegrationId, ExternalOrderNumber = externalId, OrderNumber = externalId, Status = MapOrderStatus(GetString(data, "order_status_id")), Total = GetDecimal(data, "total"), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            order.CustomerName = $"{GetString(data, "firstname")} {GetString(data, "lastname")}".Trim();
            order.CustomerEmail = GetString(data, "email");
            order.CustomerPhone = GetString(data, "telephone");

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[ExpandCartWebhook] order.add — Inserted Order {ExtId}", externalId);
        }

        private async Task HandleOrderUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "order_id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null) return;
            order.Status = MapOrderStatus(GetString(data, "order_status_id"));
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order); await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleProductCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "product_id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null) return;

            var product = new Product { Id = Guid.NewGuid(), ExternalId = externalId, StoreIntegrationId = e.StoreIntegrationId, Name = GetString(data, "name"), SKU = GetString(data, "sku"), Price = GetDecimal(data, "price"), IsActive = GetString(data, "status") == "1", Status = ProductStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            if (data.TryGetProperty("quantity", out var qEl) && qEl.TryGetInt32(out var qty)) product.Stock = qty;
            await _unitOfWork.Products.AddAsync(product); await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[ExpandCartWebhook] product.add — Inserted Product {ExtId}", externalId);
        }

        private async Task HandleProductUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "product_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null) return;
            product.Name = GetString(data, "name"); product.Price = GetDecimal(data, "price");
            if (data.TryGetProperty("quantity", out var qEl) && qEl.TryGetInt32(out var qty)) product.Stock = qty;
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product); await _unitOfWork.SaveChangesAsync();
        }

        private async Task HandleProductDeletedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.TryGetProperty("data", out var d) ? d : payload.RootElement;
            var externalId = GetString(data, "product_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products.FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null) return;
            product.IsActive = false; product.Status = ProductStatus.Deleted; product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product); await _unitOfWork.SaveChangesAsync();
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation("[ExpandCartWebhook] Event {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // OpenCart order_status_id → OrderStatus
        private static OrderStatus MapOrderStatus(string statusId) =>
            statusId switch { "1" => OrderStatus.Pending, "2" => OrderStatus.Processing, "3" => OrderStatus.Shipped, "5" => OrderStatus.Delivered, "7" => OrderStatus.Cancelled, "11" => OrderStatus.Returned, _ => OrderStatus.Pending };

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static decimal GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0m;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}