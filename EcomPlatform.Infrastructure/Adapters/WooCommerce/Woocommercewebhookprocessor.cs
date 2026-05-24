using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.WooCommerce
{
    /// <summary>
    /// يعالج WebhookEvent من WooCommerce بعد ما يتحفظ في DB.
    /// WooCommerce بيبعت الـ topic في header: X-WC-Webhook-Topic
    /// الـ payload بيكون flat JSON (نفس Shopify تقريباً بس بـ field names مختلفة)
    /// </summary>
    public sealed class WooCommerceWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WooCommerceWebhookProcessor> _logger;

        public WooCommerceWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<WooCommerceWebhookProcessor> logger)
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
                    "order.deleted" => HandleOrderDeletedAsync(webhookEvent, ct),
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
                _logger.LogError(ex,
                    "[WooWebhook] Failed to process event {Id} — type {Type}",
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
            var data = payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null)
            {
                _logger.LogInformation(
                    "[WooWebhook] order.created — Order {ExtId} already exists, skipping.", externalId);
                return;
            }

            var order = MapOrder(data, e.StoreIntegrationId, externalId);

            if (data.TryGetProperty("line_items", out var items) &&
                items.ValueKind == JsonValueKind.Array)
                foreach (var item in items.EnumerateArray())
                    order.Items.Add(MapOrderItem(item));

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[WooWebhook] order.created — Inserted Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);

            if (order == null)
            {
                _logger.LogWarning(
                    "[WooWebhook] order.updated — Order {ExtId} not found, inserting.", externalId);
                order = MapOrder(data, e.StoreIntegrationId, externalId);
                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplyOrderFields(order, data);
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[WooWebhook] order.updated — Updated Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderDeletedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (order == null)
            {
                _logger.LogWarning(
                    "[WooWebhook] order.deleted — Order {ExtId} not found.", externalId);
                return;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[WooWebhook] order.deleted — Order {ExtId} cancelled.", externalId);
        }

        // ── Products ──────────────────────────────────────────────────────────

        private async Task HandleProductCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var existing = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (existing != null)
            {
                _logger.LogInformation(
                    "[WooWebhook] product.created — Product {ExtId} already exists, skipping.", externalId);
                return;
            }

            var product = MapProduct(data, e.StoreIntegrationId, externalId);
            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[WooWebhook] product.created — Inserted Product LocalId: {LocalId}", product.Id);
        }

        private async Task HandleProductUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);

            if (product == null)
            {
                _logger.LogWarning(
                    "[WooWebhook] product.updated — Product {ExtId} not found, inserting.", externalId);
                product = MapProduct(data, e.StoreIntegrationId, externalId);
                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplyProductFields(product, data);
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[WooWebhook] product.updated — Updated Product LocalId: {LocalId}", product.Id);
        }

        private async Task HandleProductDeletedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement;
            var externalId = GetString(data, "id");
            e.ExternalEntityId = externalId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[WooWebhook] product.deleted — Product {ExtId} not found.", externalId);
                return;
            }

            product.IsActive = false;
            product.Status = ProductStatus.Deleted;
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[WooWebhook] product.deleted — Soft-deleted Product LocalId: {LocalId}", product.Id);
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "[WooWebhook] Unknown event type: {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static Order MapOrder(JsonElement data, Guid integrationId, string externalId)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                StoreIntegrationId = integrationId,
                ExternalOrderNumber = GetString(data, "number"),
                OrderNumber = GetString(data, "number"),
                Status = MapOrderStatus(GetString(data, "status")),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ApplyOrderFields(order, data);
            return order;
        }

        private static void ApplyOrderFields(Order order, JsonElement data)
        {
            order.Total = GetDecimal(data, "total");
            order.SubTotal = GetDecimal(data, "subtotal");
            order.Discount = GetDecimal(data, "discount_total");
            order.Tax = GetDecimal(data, "total_tax");
            order.Status = MapOrderStatus(GetString(data, "status"));
            order.UpdatedAt = DateTime.UtcNow;

            if (data.TryGetProperty("billing", out var billing))
            {
                order.CustomerName = $"{GetString(billing, "first_name")} {GetString(billing, "last_name")}".Trim();
                order.CustomerEmail = GetString(billing, "email");
                order.CustomerPhone = GetString(billing, "phone");
            }

            if (data.TryGetProperty("shipping", out var addr))
            {
                order.ShippingAddress = GetString(addr, "address_1");
                order.ShippingCity = GetString(addr, "city");
                order.ShippingCountry = GetString(addr, "country");
            }
        }

        private static OrderItem MapOrderItem(JsonElement item) => new()
        {
            Id = Guid.NewGuid(),
            ExternalId = GetString(item, "id"),
            ExternalProductId = GetString(item, "product_id"),
            ProductName = GetString(item, "name"),
            ProductSKU = GetString(item, "sku"),
            Quantity = item.TryGetProperty("quantity", out var q) &&
                              q.TryGetInt32(out var qty) ? qty : 1,
            UnitPrice = GetDecimal(item, "price"),
            TotalPrice = GetDecimal(item, "total"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        private static Product MapProduct(JsonElement data, Guid integrationId, string externalId)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                StoreIntegrationId = integrationId,
                IsActive = true,
                Status = ProductStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ApplyProductFields(product, data);
            return product;
        }

        private static void ApplyProductFields(Product product, JsonElement data)
        {
            product.Name = GetString(data, "name");
            product.Description = GetString(data, "description");
            product.SKU = GetString(data, "sku");
            product.Price = GetDecimal(data, "price");
            product.TrackInventory = true;
            product.UpdatedAt = DateTime.UtcNow;

            if (data.TryGetProperty("stock_quantity", out var sq) &&
                sq.TryGetInt32(out var stock))
                product.Stock = stock;

            var status = GetString(data, "status");
            product.IsActive = status == "publish";
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapOrderStatus(string status) =>
            status switch
            {
                "pending" => OrderStatus.Pending,
                "processing" => OrderStatus.Processing,
                "on-hold" => OrderStatus.Pending,
                "completed" => OrderStatus.Delivered,
                "cancelled" => OrderStatus.Cancelled,
                "refunded" => OrderStatus.Returned,
                "failed" => OrderStatus.Cancelled,
                _ => OrderStatus.Pending
            };

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static decimal GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) &&
            v.ValueKind == JsonValueKind.Number &&
            v.TryGetDecimal(out var d) ? d : 0m;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}