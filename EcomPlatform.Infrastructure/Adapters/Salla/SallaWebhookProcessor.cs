using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.Salla
{
    /// <summary>
    /// يعالج WebhookEvent بعد ما يتحفظ في DB.
    /// كل event type ليه handler خاص بيه.
    ///
    /// المتطلبات في الـ Entities:
    ///   - Product  : ExternalId (string), StoreIntegrationId (Guid)
    ///   - Order    : ExternalId (string), StoreIntegrationId (Guid), ExternalOrderNumber (string)
    ///   - OrderItem: ExternalId (string), ExternalProductId (string)
    ///
    /// المتطلبات في الـ IUnitOfWork:
    ///   - Products.FindByExternalIdAsync(externalId, integrationId)
    ///   - Orders.FindByExternalIdAsync(externalId, integrationId)
    /// </summary>
    public class SallaWebhookProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SallaWebhookProcessor> _logger;

        public SallaWebhookProcessor(
            IUnitOfWork unitOfWork,
            ILogger<SallaWebhookProcessor> logger)
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
                    "order.status.updated" => HandleOrderStatusUpdatedAsync(webhookEvent, ct),
                    "product.created" => HandleProductCreatedAsync(webhookEvent, ct),
                    "product.updated" => HandleProductUpdatedAsync(webhookEvent, ct),
                    "product.deleted" => HandleProductDeletedAsync(webhookEvent, ct),
                    "quantity.updated" => HandleInventoryUpdatedAsync(webhookEvent, ct),
                    _ => HandleUnknownEventAsync(webhookEvent, ct)
                });

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process webhook {Id} — type {Type}",
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

            // idempotency — لو الـ order موجود ما نعملش insert تاني
            var existing = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalOrderId, e.StoreIntegrationId);

            if (existing != null)
            {
                _logger.LogInformation(
                    "order.created — Order {ExtId} already exists (LocalId: {LocalId}), skipping insert.",
                    externalOrderId, existing.Id);
                return;
            }

            var order = MapSallaOrder(data, e.StoreIntegrationId, externalOrderId);

            // Items
            if (data.TryGetProperty("items", out var itemsEl) &&
                itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsEl.EnumerateArray())
                    order.Items.Add(MapSallaOrderItem(item));
            }

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "order.created — Inserted Order LocalId: {LocalId}, ExternalId: {ExtId}",
                order.Id, externalOrderId);
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
                // order جه updated قبل created (race) — نعمل insert
                _logger.LogWarning(
                    "order.updated — Order {ExtId} not found locally, inserting.",
                    externalOrderId);

                order = MapSallaOrder(data, e.StoreIntegrationId, externalOrderId);
                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplySallaOrderFields(order, data);

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "order.updated — Updated Order LocalId: {LocalId}", order.Id);
        }

        private async Task HandleOrderStatusUpdatedAsync(WebhookEvent e, CancellationToken ct)
        {
            using var payload = ParsePayload(e.RawPayload);
            var data = payload.RootElement.GetProperty("data");

            var externalOrderId = GetString(data, "id");
            var sallaStatus = GetString(data, "status");
            e.ExternalEntityId = externalOrderId;

            var order = await _unitOfWork.Orders
                .FindByExternalIdAsync(externalOrderId, e.StoreIntegrationId);

            if (order == null)
            {
                _logger.LogWarning(
                    "order.status.updated — Order {ExtId} not found locally, cannot update status.",
                    externalOrderId);
                return;
            }

            order.Status = MapSallaOrderStatus(sallaStatus);
            order.UpdatedAt = DateTime.UtcNow;

            // حدّث timestamps المناسبة
            if (order.Status == OrderStatus.Shipped && order.ShippedAt == null)
                order.ShippedAt = DateTime.UtcNow;
            if (order.Status == OrderStatus.Delivered && order.DeliveredAt == null)
                order.DeliveredAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "order.status.updated — Order {ExtId} → Status: {Status}",
                externalOrderId, order.Status);
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
                    "product.created — Product {ExtId} already exists (LocalId: {LocalId}), skipping.",
                    externalProductId, existing.Id);
                return;
            }

            var product = MapSallaProduct(data, e.StoreIntegrationId, externalProductId);

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "product.created — Inserted Product LocalId: {LocalId}, ExternalId: {ExtId}",
                product.Id, externalProductId);
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
                    "product.updated — Product {ExtId} not found locally, inserting.",
                    externalProductId);

                product = MapSallaProduct(data, e.StoreIntegrationId, externalProductId);
                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            ApplySallaProductFields(product, data);

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "product.updated — Updated Product LocalId: {LocalId}", product.Id);
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
                    "product.deleted — Product {ExtId} not found locally, nothing to delete.",
                    externalProductId);
                return;
            }

            // Soft delete
            product.IsActive = false;
            product.Status = ProductStatus.Deleted;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "product.deleted — Soft-deleted Product LocalId: {LocalId}", product.Id);
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
                    "quantity.updated — Product {ExtId} not found locally, cannot update stock.",
                    externalProductId);
                return;
            }

            // Salla بيبعت quantity جوا quantities[] أو مباشرة في quantity
            int newStock = product.Stock;

            if (data.TryGetProperty("quantities", out var quantitiesEl) &&
                quantitiesEl.ValueKind == JsonValueKind.Array)
            {
                // جمع كل الـ available quantities من جميع المخازن
                newStock = 0;
                foreach (var q in quantitiesEl.EnumerateArray())
                {
                    if (q.TryGetProperty("available", out var av) &&
                        av.TryGetInt32(out var avInt))
                        newStock += avInt;
                }
            }
            else if (data.TryGetProperty("quantity", out var qEl) &&
                     qEl.TryGetInt32(out var directQty))
            {
                newStock = directQty;
            }

            product.Stock = newStock;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "quantity.updated — Product {ExtId} stock → {Stock}",
                externalProductId, newStock);
        }

        // ── Unknown handler ───────────────────────────────────────────────────

        private Task HandleUnknownEventAsync(WebhookEvent e, CancellationToken ct)
        {
            _logger.LogInformation(
                "Salla unknown event type: {Type} — saved but not processed",
                e.EventType);

            return Task.CompletedTask;
        }

        // ── Mapping helpers ───────────────────────────────────────────────────

        private static Order MapSallaOrder(
            JsonElement data,
            Guid storeIntegrationId,
            string externalOrderId)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                ExternalId = externalOrderId,
                StoreIntegrationId = storeIntegrationId,
                ExternalOrderNumber = GetString(data, "reference_id"),
                OrderNumber = GetString(data, "reference_id"),
                Status = MapSallaOrderStatus(GetString(data, "status")),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            ApplySallaOrderFields(order, data);
            return order;
        }

        private static void ApplySallaOrderFields(Order order, JsonElement data)
        {
            // Totals
            if (data.TryGetProperty("amounts", out var amounts))
            {
                order.SubTotal = GetDecimal(amounts, "subtotal");
                order.ShippingCost = GetDecimal(amounts, "shipping");
                order.Discount = GetDecimal(amounts, "discount");
                order.Tax = GetDecimal(amounts, "tax");
                order.Total = GetDecimal(amounts, "total");
            }

            // Customer info
            if (data.TryGetProperty("customer", out var customer))
            {
                order.CustomerName = GetString(customer, "name");
                order.CustomerEmail = GetString(customer, "email");
                order.CustomerPhone = GetString(customer, "mobile");
            }

            // Shipping address
            if (data.TryGetProperty("shipping", out var shipping))
            {
                order.ShippingAddress = GetString(shipping, "street");
                order.ShippingCity = GetString(shipping, "city");
                order.ShippingCountry = GetString(shipping, "country");
                order.ShippingPhone = GetString(shipping, "phone");
            }

            // Payment
            if (data.TryGetProperty("payment_method", out var pm))
                order.PaymentStatus = PaymentStatus.Pending; // يتحدث من payment webhook

            order.UpdatedAt = DateTime.UtcNow;
        }

        private static OrderItem MapSallaOrderItem(JsonElement item)
        {
            var externalProductId = GetString(item, "product_id");

            return new OrderItem
            {
                Id = Guid.NewGuid(),
                ExternalId = GetString(item, "id"),
                ExternalProductId = externalProductId,
                ProductName = GetString(item, "name"),
                ProductSKU = GetString(item, "sku"),
                ProductImage = GetString(item, "thumbnail"),
                Quantity = item.TryGetProperty("quantity", out var q) &&
                                  q.TryGetInt32(out var qty) ? qty : 1,
                UnitPrice = GetDecimal(item, "price"),
                TotalPrice = GetDecimal(item, "total"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        private static Product MapSallaProduct(
            JsonElement data,
            Guid storeIntegrationId,
            string externalProductId)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                ExternalId = externalProductId,
                StoreIntegrationId = storeIntegrationId,
                IsActive = true,
                Status = ProductStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            ApplySallaProductFields(product, data);
            return product;
        }

        private static void ApplySallaProductFields(Product product, JsonElement data)
        {
            product.Name = GetString(data, "name");
            product.SKU = GetString(data, "sku");
            product.Barcode = GetString(data, "barcode");
            product.Description = GetString(data, "description");
            product.ShortDescription = GetString(data, "short_description");
            product.Slug = GetString(data, "url");       // أو slug
            product.MetaTitle = GetString(data, "name");      // fallback
            product.Weight = GetDecimal(data, "weight");
            product.TrackInventory = true;
            product.UpdatedAt = DateTime.UtcNow;

            // Price — Salla بيبعت price كـ object { amount, currency }
            if (data.TryGetProperty("price", out var priceEl))
            {
                product.Price = priceEl.ValueKind == JsonValueKind.Object
                    ? GetDecimal(priceEl, "amount")
                    : GetDecimalDirect(priceEl);
            }

            if (data.TryGetProperty("sale_price", out var saleEl))
            {
                var sale = saleEl.ValueKind == JsonValueKind.Object
                    ? GetDecimal(saleEl, "amount")
                    : GetDecimalDirect(saleEl);
                if (sale > 0)
                    product.ComparePrice = product.Price;
                product.Price = sale > 0 ? sale : product.Price;
            }

            // Stock
            if (data.TryGetProperty("quantity", out var qEl) &&
                qEl.TryGetInt32(out var stock))
                product.Stock = stock;

            // Status
            var status = GetString(data, "status");
            product.IsActive = status is "sale" or "out" or "";
            product.Status = status switch
            {
                "sale" => ProductStatus.Active,
                "out" => ProductStatus.OutOfStock,
                "hidden" => ProductStatus.Inactive,
                "deleted" => ProductStatus.Deleted,
                _ => ProductStatus.Active
            };

            // Main image
            if (data.TryGetProperty("images", out var imagesEl) &&
                imagesEl.ValueKind == JsonValueKind.Array)
            {
                product.Images.Clear();
                var sort = 0;
                foreach (var img in imagesEl.EnumerateArray())
                {
                    product.Images.Add(new ProductImage
                    {
                        Id = Guid.NewGuid(),
                        Url = GetString(img, "url"),
                        Alt = GetString(img, "alt"),
                        IsMain = sort == 0,
                        SortOrder = sort++,
                    });
                }
            }
        }

        // ── Status mapping ────────────────────────────────────────────────────

        private static OrderStatus MapSallaOrderStatus(string sallaStatus) =>
            sallaStatus switch
            {
                "pending" => OrderStatus.Pending,
                "under_review" => OrderStatus.Processing,
                "in_progress" => OrderStatus.Processing,
                "ready_for_shipment" => OrderStatus.Processing,
                "shipping" => OrderStatus.Shipped,
                "delivered" => OrderStatus.Delivered,
                "cancelled" => OrderStatus.Cancelled,
                "returned" => OrderStatus.Returned,
                _ => OrderStatus.Pending
            };

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static decimal GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? GetDecimalDirect(v) : 0m;

        private static decimal GetDecimalDirect(JsonElement v) =>
            v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : 0m;

        private static JsonDocument ParsePayload(string raw)
        {
            try { return JsonDocument.Parse(raw); }
            catch { return JsonDocument.Parse("{}"); }
        }
    }
}