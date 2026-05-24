using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.Zid
{
    /// <summary>
    /// يعالج WebhookEvent من Zid بعد ما يتحفظ في DB.
    /// نفس pattern الـ SallaWebhookProcessor تماماً.
    /// </summary>
    public sealed class ZidWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ZidWebhookProcessor> _logger;

        public ZidWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<ZidWebhookProcessor> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ── Entry point ───────────────────────────────────────────────────────

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
                    "order.canceled" => HandleOrderCanceledAsync(webhookEvent, ct),
                    "product.created" => HandleProductCreatedAsync(webhookEvent, ct),
                    "product.updated" => HandleProductUpdatedAsync(webhookEvent, ct),
                    "product.deleted" => HandleProductDeletedAsync(webhookEvent, ct),
                    "inventory.updated" => HandleInventoryUpdatedAsync(webhookEvent, ct),
                    _ => HandleUnknownEventAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[ZidWebhook] Failed to process event {Id} — type {Type}",
                    webhookEventId, webhookEvent.EventType);

                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.ErrorMessage = ex.Message;
            }

            await _unitOfWork.WebhookEvents.UpdateAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Order handlers ────────────────────────────────────────────────────

        private async Task HandleOrderCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalOrderId = GetString(data, "id");
            e.ExternalEntityId = externalOrderId;

            var existing = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalOrderId, e.StoreIntegrationId);

            if (existing != null)
            {
                _logger.LogInformation(
                    "[ZidWebhook] order.created — Order {ExtId} already exists, skipping.",
                    externalOrderId);
                return;
            }

            var order = MapZidOrder(data, e.StoreIntegrationId, externalOrderId);

            if (data.TryGetProperty("products", out var itemsEl) &&
                itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsEl.EnumerateArray())
                    order.Items.Add(MapZidOrderItem(item));
            }

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ZidWebhook] order.created — Inserted Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalOrderId = GetString(data, "id");
            e.ExternalEntityId = externalOrderId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalOrderId, e.StoreIntegrationId);

            if (order == null)
            {
                _logger.LogWarning(
                    "[ZidWebhook] order.updated — Order {ExtId} not found, inserting.",
                    externalOrderId);
                order = MapZidOrder(data, e.StoreIntegrationId, externalOrderId);
                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplyZidOrderFields(order, data);
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ZidWebhook] order.updated — Updated Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderCanceledAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalOrderId = GetString(data, "id");
            e.ExternalEntityId = externalOrderId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalOrderId, e.StoreIntegrationId);

            if (order == null)
            {
                _logger.LogWarning(
                    "[ZidWebhook] order.canceled — Order {ExtId} not found.",
                    externalOrderId);
                return;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ZidWebhook] order.canceled — Order {ExtId} cancelled.", externalOrderId);
        }

        // ── Product handlers ──────────────────────────────────────────────────

        private async Task HandleProductCreatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalProductId = GetString(data, "id");
            e.ExternalEntityId = externalProductId;

            var existing = await _unitOfWork.Products
                .FindByExternalIdAsync(externalProductId, e.StoreIntegrationId);

            if (existing != null)
            {
                _logger.LogInformation(
                    "[ZidWebhook] product.created — Product {ExtId} already exists, skipping.",
                    externalProductId);
                return;
            }

            var product = MapZidProduct(data, e.StoreIntegrationId, externalProductId);
            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ZidWebhook] product.created — Inserted Product LocalId: {LocalId}", product.Id);
        }

        private async Task HandleProductUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalProductId = GetString(data, "id");
            e.ExternalEntityId = externalProductId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalProductId, e.StoreIntegrationId);

            if (product == null)
            {
                _logger.LogWarning(
                    "[ZidWebhook] product.updated — Product {ExtId} not found, inserting.",
                    externalProductId);
                product = MapZidProduct(data, e.StoreIntegrationId, externalProductId);
                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplyZidProductFields(product, data);
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ZidWebhook] product.updated — Updated Product LocalId: {LocalId}", product.Id);
        }

        private async Task HandleProductDeletedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalProductId = GetString(data, "id");
            e.ExternalEntityId = externalProductId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalProductId, e.StoreIntegrationId);

            if (product == null)
            {
                _logger.LogWarning(
                    "[ZidWebhook] product.deleted — Product {ExtId} not found.", externalProductId);
                return;
            }

            product.IsActive = false;
            product.Status = ProductStatus.Deleted;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[ZidWebhook] product.deleted — Soft-deleted Product LocalId: {LocalId}", product.Id);
        }

        // ── Inventory handler ─────────────────────────────────────────────────

        private async Task HandleInventoryUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");
            var externalProductId = GetString(data, "id");
            e.ExternalEntityId = externalProductId;

            var product = await _unitOfWork.Products
                .FindByExternalIdAsync(externalProductId, e.StoreIntegrationId);

            if (product == null)
            {
                _logger.LogWarning(
                    "[ZidWebhook] inventory.updated — Product {ExtId} not found.", externalProductId);
                return;
            }

            if (data.TryGetProperty("quantity", out var qEl) &&
                qEl.TryGetInt32(out var qty))
            {
                product.Stock = qty;
                product.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.Products.UpdateAsync(product);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "[ZidWebhook] inventory.updated — Product {ExtId} stock → {Stock}",
                    externalProductId, qty);
            }
        }

        // ── Unknown ───────────────────────────────────────────────────────────

        private Task HandleUnknownEventAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "[ZidWebhook] Unknown event type: {Type} — saved but not processed", e.EventType);
            return Task.CompletedTask;
        }

        // ── Mapping helpers ───────────────────────────────────────────────────

        private static Order MapZidOrder(JsonElement data, Guid integrationId, string externalId)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                StoreIntegrationId = integrationId,
                ExternalOrderNumber = GetString(data, "code"),
                OrderNumber = GetString(data, "code"),
                Status = MapZidOrderStatus(GetString(data, "status")),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ApplyZidOrderFields(order, data);
            return order;
        }

        private static void ApplyZidOrderFields(Order order, JsonElement data)
        {
            order.Total = GetDecimal(data, "total");
            order.SubTotal = GetDecimal(data, "subtotal");
            order.Discount = GetDecimal(data, "discount");
            order.Tax = GetDecimal(data, "tax");

            if (data.TryGetProperty("customer", out var customer))
            {
                order.CustomerName = GetString(customer, "name");
                order.CustomerEmail = GetString(customer, "email");
                order.CustomerPhone = GetString(customer, "phone");
            }

            if (data.TryGetProperty("shipping_address", out var addr))
            {
                order.ShippingAddress = GetString(addr, "street");
                order.ShippingCity = GetString(addr, "city");
                order.ShippingCountry = GetString(addr, "country");
            }

            order.Status = MapZidOrderStatus(GetString(data, "status"));
            order.UpdatedAt = DateTime.UtcNow;
        }

        private static OrderItem MapZidOrderItem(JsonElement item) => new()
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

        private static Product MapZidProduct(JsonElement data, Guid integrationId, string externalId)
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
            ApplyZidProductFields(product, data);
            return product;
        }

        private static void ApplyZidProductFields(Product product, JsonElement data)
        {
            product.Name = GetString(data, "name");
            product.SKU = GetString(data, "sku");
            product.Description = GetString(data, "description");
            product.Price = GetDecimal(data, "price");
            product.TrackInventory = true;
            product.UpdatedAt = DateTime.UtcNow;

            if (data.TryGetProperty("quantity", out var qEl) &&
                qEl.TryGetInt32(out var stock))
                product.Stock = stock;

            var active = GetString(data, "active");
            product.IsActive = active != "0";
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapZidOrderStatus(string status) =>
            status switch
            {
                "new" => OrderStatus.Pending,
                "processing" => OrderStatus.Processing,
                "shipped" => OrderStatus.Shipped,
                "delivered" => OrderStatus.Delivered,
                "canceled" => OrderStatus.Cancelled,
                "returned" => OrderStatus.Returned,
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