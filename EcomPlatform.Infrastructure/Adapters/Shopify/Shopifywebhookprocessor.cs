using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.Shopify
{
    /// <summary>
    /// يعالج WebhookEvent من Shopify بعد ما يتحفظ في DB.
    /// Shopify بيبعت الـ topic في header: X-Shopify-Topic
    /// الـ payload بيكون flat JSON مش متغلف بـ "data"
    /// </summary>
    public sealed class ShopifyWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ShopifyWebhookProcessor> _logger;

        public ShopifyWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<ShopifyWebhookProcessor> logger)
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
                    "orders/create" => HandleOrderCreatedAsync(webhookEvent, ct),
                    "orders/updated" => HandleOrderUpdatedAsync(webhookEvent, ct),
                    "orders/cancelled" => HandleOrderCancelledAsync(webhookEvent, ct),
                    "products/create" => HandleProductCreatedAsync(webhookEvent, ct),
                    "products/update" => HandleProductUpdatedAsync(webhookEvent, ct),
                    "products/delete" => HandleProductDeletedAsync(webhookEvent, ct),
                    "inventory_levels/update" => HandleInventoryUpdatedAsync(webhookEvent, ct),
                    _ => HandleUnknownAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[ShopifyWebhook] Failed to process event {Id} — type {Type}",
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
                    "[ShopifyWebhook] orders/create — Order {ExtId} already exists, skipping.", externalId);
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
                "[ShopifyWebhook] orders/create — Inserted Order LocalId: {LocalId}", order.Id);
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
                    "[ShopifyWebhook] orders/updated — Order {ExtId} not found, inserting.", externalId);
                order = MapOrder(data, e.StoreIntegrationId, externalId);
                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplyOrderFields(order, data);
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ShopifyWebhook] orders/updated — Updated Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderCancelledAsync(WebhookEvent e, CancellationToken ct)
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
                    "[ShopifyWebhook] orders/cancelled — Order {ExtId} not found.", externalId);
                return;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ShopifyWebhook] orders/cancelled — Order {ExtId} cancelled.", externalId);
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
                    "[ShopifyWebhook] products/create — Product {ExtId} already exists, skipping.", externalId);
                return;
            }

            var product = MapProduct(data, e.StoreIntegrationId, externalId);
            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ShopifyWebhook] products/create — Inserted Product LocalId: {LocalId}", product.Id);
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
                    "[ShopifyWebhook] products/update — Product {ExtId} not found, inserting.", externalId);
                product = MapProduct(data, e.StoreIntegrationId, externalId);
                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplyProductFields(product, data);
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ShopifyWebhook] products/update — Updated Product LocalId: {LocalId}", product.Id);
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
                    "[ShopifyWebhook] products/delete — Product {ExtId} not found.", externalId);
                return;
            }

            product.IsActive = false;
            product.Status = ProductStatus.Deleted;
            product.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ShopifyWebhook] products/delete — Soft-deleted Product LocalId: {LocalId}", product.Id);
        }

        // ── Inventory ─────────────────────────────────────────────────────────

        private async Task HandleInventoryUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement;

            // Shopify بيبعت inventory_item_id مش product_id مباشرة
            var inventoryItemId = GetString(data, "inventory_item_id");
            e.ExternalEntityId = inventoryItemId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(inventoryItemId, e.StoreIntegrationId);
            if (product == null)
            {
                _logger.LogWarning(
                    "[ShopifyWebhook] inventory_levels/update — InventoryItem {ExtId} not found.", inventoryItemId);
                return;
            }

            if (data.TryGetProperty("available", out var avail) &&
                avail.TryGetInt32(out var qty))
            {
                product.Stock = qty;
                product.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Products.UpdateAsync(product);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "[ShopifyWebhook] inventory_levels/update — InventoryItem {ExtId} stock → {Stock}",
                    inventoryItemId, qty);
            }
        }

        private Task HandleUnknownAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "[ShopifyWebhook] Unknown event type: {Type} — saved but not processed", e.EventType);
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
                ExternalOrderNumber = GetString(data, "order_number"),
                OrderNumber = GetString(data, "order_number"),
                Status = MapOrderStatus(GetString(data, "financial_status")),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ApplyOrderFields(order, data);
            return order;
        }

        private static void ApplyOrderFields(Order order, JsonElement data)
        {
            order.Total = GetDecimal(data, "total_price");
            order.SubTotal = GetDecimal(data, "subtotal_price");
            order.Discount = GetDecimal(data, "total_discounts");
            order.Tax = GetDecimal(data, "total_tax");
            order.Status = MapOrderStatus(GetString(data, "financial_status"));
            order.UpdatedAt = DateTime.UtcNow;

            if (data.TryGetProperty("customer", out var customer))
            {
                order.CustomerName = $"{GetString(customer, "first_name")} {GetString(customer, "last_name")}".Trim();
                order.CustomerEmail = GetString(customer, "email");
                order.CustomerPhone = GetString(customer, "phone");
            }

            if (data.TryGetProperty("shipping_address", out var addr))
            {
                order.ShippingAddress = GetString(addr, "address1");
                order.ShippingCity = GetString(addr, "city");
                order.ShippingCountry = GetString(addr, "country");
            }
        }

        private static OrderItem MapOrderItem(JsonElement item) => new()
        {
            Id = Guid.NewGuid(),
            ExternalId = GetString(item, "id"),
            ExternalProductId = GetString(item, "product_id"),
            ProductName = GetString(item, "title"),
            ProductSKU = GetString(item, "sku"),
            Quantity = item.TryGetProperty("quantity", out var q) &&
                              q.TryGetInt32(out var qty) ? qty : 1,
            UnitPrice = GetDecimal(item, "price"),
            TotalPrice = GetDecimal(item, "price") *
                         (item.TryGetProperty("quantity", out var qq) &&
                          qq.TryGetInt32(out var qqty) ? qqty : 1),
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
            product.Name = GetString(data, "title");
            product.Description = GetString(data, "body_html");
            product.UpdatedAt = DateTime.UtcNow;

            var status = GetString(data, "status");
            product.IsActive = status == "active";
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;

            // الـ SKU والـ price بيكونوا في أول variant
            if (data.TryGetProperty("variants", out var variants) &&
                variants.ValueKind == JsonValueKind.Array)
            {
                var first = variants.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    product.SKU = GetString(first, "sku");
                    product.Price = GetDecimal(first, "price");

                    if (first.TryGetProperty("inventory_quantity", out var iq) &&
                        iq.TryGetInt32(out var stock))
                        product.Stock = stock;
                }
            }
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapOrderStatus(string status) =>
            status switch
            {
                "pending" => OrderStatus.Pending,
                "paid" => OrderStatus.Processing,
                "partially_paid" => OrderStatus.Processing,
                "refunded" => OrderStatus.Returned,
                "voided" => OrderStatus.Cancelled,
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