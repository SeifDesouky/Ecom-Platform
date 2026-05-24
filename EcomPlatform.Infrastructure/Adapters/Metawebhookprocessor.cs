using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.Meta
{
    /// <summary>
    /// يعالج WebhookEvent من منصات Meta (Instagram Shop + Facebook Shop + WhatsApp Catalog).
    /// الـ 3 منصات بيشتركوا في نفس الـ Meta webhook structure:
    ///   { "object": "...", "entry": [{ "changes": [{ "field": "...", "value": {...} }] }] }
    /// الـ object بيحدد المنصة: "instagram" | "page" (facebook) | "whatsapp_business_account"
    /// </summary>
    public sealed class MetaWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MetaWebhookProcessor> _logger;

        public MetaWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<MetaWebhookProcessor> logger)
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
                    "orders/created" => HandleOrderCreatedAsync(webhookEvent, ct),
                    "orders/updated" => HandleOrderUpdatedAsync(webhookEvent, ct),
                    "catalog/product_updated" => HandleProductUpdatedAsync(webhookEvent, ct),
                    "catalog/product_deleted" => HandleProductDeletedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[MetaWebhook] Failed to process event {Id} — type {Type}",
                    webhookEventId, webhookEvent.EventType);

                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Orders ────────────────────────────────────────────────────────────

        private async Task HandleOrderCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var value = ExtractValue(payload.RootElement);
            if (value.ValueKind == JsonValueKind.Undefined) return;

            var externalId = GetString(value, "id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null)
            {
                _logger.LogInformation(
                    "[MetaWebhook] orders/created — Order {ExtId} already exists, skipping.", externalId);
                return;
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                StoreIntegrationId = e.StoreIntegrationId,
                ExternalOrderNumber = externalId,
                OrderNumber = externalId,
                Status = OrderStatus.Pending,
                Total = GetDecimal(value, "estimated_payment_details.total_amount.amount"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            if (value.TryGetProperty("buyer_details", out var buyer))
            {
                order.CustomerName = GetString(buyer, "name");
                order.CustomerEmail = GetString(buyer, "email");
            }

            if (value.TryGetProperty("items", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    order.Items.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ExternalId = GetString(item, "id"),
                        ExternalProductId = GetString(item, "retailer_id"),
                        ProductName = GetString(item, "name"),
                        Quantity = item.TryGetProperty("quantity", out var q) &&
                                          q.TryGetInt32(out var qty) ? qty : 1,
                        UnitPrice = GetDecimal(item, "retailer_product_item.sale_price"),
                        TotalPrice = GetDecimal(item, "retailer_product_item.sale_price"),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
            }

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[MetaWebhook] orders/created — Inserted Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var value = ExtractValue(payload.RootElement);
            if (value.ValueKind == JsonValueKind.Undefined) return;

            var externalId = GetString(value, "id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null)
            {
                _logger.LogWarning(
                    "[MetaWebhook] orders/updated — Order {ExtId} not found.", externalId);
                return;
            }

            var status = GetString(value, "order_status");
            order.Status = MapOrderStatus(status);
            order.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[MetaWebhook] orders/updated — Order {ExtId} → {Status}", externalId, status);
        }

        // ── Products (Catalog) ────────────────────────────────────────────────

        private async Task HandleProductUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var value = ExtractValue(payload.RootElement);
            if (value.ValueKind == JsonValueKind.Undefined) return;

            var externalId = GetString(value, "retailer_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[MetaWebhook] catalog/product_updated — Product {ExtId} not found, inserting.", externalId);
                product = new Product
                {
                    Id = Guid.NewGuid(),
                    ExternalId = externalId,
                    StoreIntegrationId = e.StoreIntegrationId,
                    IsActive = true,
                    Status = ProductStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                product.Name = GetString(value, "name");
                product.Price = GetDecimal(value, "sale_price");
                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            product.Name = GetString(value, "name");
            product.Price = GetDecimal(value, "sale_price");
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[MetaWebhook] catalog/product_updated — Product {ExtId} updated.", externalId);
        }

        private async Task HandleProductDeletedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var value = ExtractValue(payload.RootElement);
            if (value.ValueKind == JsonValueKind.Undefined) return;

            var externalId = GetString(value, "retailer_id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[MetaWebhook] catalog/product_deleted — Product {ExtId} not found.", externalId);
                return;
            }

            product.IsActive = false;
            product.Status = ProductStatus.Deleted;
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[MetaWebhook] catalog/product_deleted — Soft-deleted Product LocalId: {LocalId}", product.Id);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "[MetaWebhook] Unknown event type: {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // ── Meta payload structure helper ─────────────────────────────────────
        // Meta بيبعت: { "entry": [{ "changes": [{ "value": {...} }] }] }
        private static JsonElement ExtractValue(JsonElement root)
        {
            if (root.TryGetProperty("entry", out var entries) &&
                entries.ValueKind == JsonValueKind.Array)
            {
                var entry = entries.EnumerateArray().FirstOrDefault();
                if (entry.ValueKind != JsonValueKind.Undefined &&
                    entry.TryGetProperty("changes", out var changes) &&
                    changes.ValueKind == JsonValueKind.Array)
                {
                    var change = changes.EnumerateArray().FirstOrDefault();
                    if (change.ValueKind != JsonValueKind.Undefined &&
                        change.TryGetProperty("value", out var value))
                        return value;
                }
            }
            return default;
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapOrderStatus(string status) =>
            status switch
            {
                "CREATED" => OrderStatus.Pending,
                "IN_PROGRESS" => OrderStatus.Processing,
                "SHIPPED" => OrderStatus.Shipped,
                "COMPLETED" => OrderStatus.Delivered,
                "CANCELLED" => OrderStatus.Cancelled,
                "REFUNDED" => OrderStatus.Returned,
                _ => OrderStatus.Pending
            };

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static decimal GetDecimal(JsonElement el, string prop)
        {
            // بيدعم nested props زي "a.b.c"
            var parts = prop.Split('.');
            var current = el;
            foreach (var part in parts)
            {
                if (!current.TryGetProperty(part, out current))
                    return 0m;
            }
            return current.ValueKind == JsonValueKind.Number &&
                   current.TryGetDecimal(out var d) ? d : 0m;
        }

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}