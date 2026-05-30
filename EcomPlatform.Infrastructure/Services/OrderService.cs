using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Orders;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EcomPlatform.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoyaltyService _loyaltyService;

        public OrderService(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IServiceProvider serviceProvider,
            ILoyaltyService loyaltyService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
            _loyaltyService = loyaltyService;
        }

        public async Task<ApiResponse<OrderResponseDto>> CreateAsync(CreateOrderDto dto)
        {
            // جيب كل الـ products بـ query واحدة بدل N+1
            var productIds = dto.Items.Select(i => i.ProductId).ToHashSet();
            var allProducts = await _unitOfWork.Products.FindAsync(p => productIds.Contains(p.Id));
            var productMap = allProducts.ToDictionary(p => p.Id);

            decimal subTotal = 0;
            var orderItems = new List<OrderItem>();

            foreach (var item in dto.Items)
            {
                if (!productMap.TryGetValue(item.ProductId, out var product))
                    return ApiResponse<OrderResponseDto>.Fail($"Product {item.ProductId} not found");

                if (product.TrackInventory && product.Stock < item.Quantity)
                    return ApiResponse<OrderResponseDto>.Fail($"Insufficient stock for {product.Name}");

                var totalPrice = item.UnitPrice * item.Quantity;
                subTotal += totalPrice;

                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    ProductSKU = product.SKU,
                    ProductImage = product.Images?.FirstOrDefault(i => i.IsMain)?.Url ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = totalPrice
                });
            }

            var total = subTotal + dto.ShippingCost + dto.Tax - dto.Discount;

            var order = new Order
            {
                OrderNumber = GenerateOrderNumber(),
                TenantId = dto.TenantId,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                CustomerEmail = dto.CustomerEmail,
                CustomerPhone = dto.CustomerPhone,
                ShippingAddress = dto.ShippingAddress,
                ShippingCity = dto.ShippingCity,
                ShippingCountry = dto.ShippingCountry,
                ShippingPhone = dto.ShippingPhone,
                ShippingCost = dto.ShippingCost,
                Discount = dto.Discount,
                Tax = dto.Tax,
                Notes = dto.Notes,
                SubTotal = subTotal,
                Total = total,
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                Items = orderItems
            };

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            if (!string.IsNullOrEmpty(order.CustomerEmail))
            {
                _ = _emailService.SendOrderConfirmationAsync(
                    order.CustomerEmail,
                    order.CustomerName,
                    order.OrderNumber,
                    order.Total);
            }

            return ApiResponse<OrderResponseDto>.Ok(MapToDto(order), "Order created successfully");
        }

        public async Task<ApiResponse<OrderResponseDto>> GetByIdAsync(Guid id)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<OrderResponseDto>.Fail("Order not found");

            return ApiResponse<OrderResponseDto>.Ok(MapToDto(order));
        }

        public async Task<ApiResponse<PagedResponse<OrderResponseDto>>> GetAllByTenantAsync(
            Guid tenantId,
            PaginationParams pagination)
        {
            var all = await _unitOfWork.Orders.FindAsync(o => o.TenantId == tenantId);
            var totalCount = all.Count();
            var items = all
                .OrderByDescending(o => o.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .Select(MapToDto)
                .ToList();
            var result = PagedResponse<OrderResponseDto>.Create(items, totalCount, pagination);
            return ApiResponse<PagedResponse<OrderResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, OrderStatus status)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<bool>.Fail("Order not found");

            order.Status = status;

            if (status == OrderStatus.Shipped)
                order.ShippedAt = DateTime.UtcNow;
            else if (status == OrderStatus.Delivered)
                order.DeliveredAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            // منح نقاط الولاء عند التسليم
            if (status == OrderStatus.Delivered && order.CustomerId.HasValue)
            {
                await _loyaltyService.EarnFromOrderAsync(
                    order.TenantId ?? Guid.Empty,
                    order.CustomerId.Value,
                    order.Id,
                    order.Total);
            }

            return ApiResponse<bool>.Ok(true, "Order status updated successfully");
        }

        public async Task<ApiResponse<bool>> UpdatePaymentStatusAsync(Guid id, PaymentStatus status)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<bool>.Fail("Order not found");

            order.PaymentStatus = status;

            if (status == PaymentStatus.Paid)
            {
                order.PaidAt = DateTime.UtcNow;

                // خصم الـ stock بعد إتمام الدفع
                if (order.Items != null)
                {
                    var productIds = order.Items.Select(i => i.ProductId).ToHashSet();
                    var products = await _unitOfWork.Products
                        .FindAsync(p => productIds.Contains(p.Id));
                    var productMap = products.ToDictionary(p => p.Id);

                    foreach (var item in order.Items)
                    {
                        if (!productMap.TryGetValue(item.ProductId, out var product)) continue;
                        if (!product.TrackInventory) continue;

                        if (product.Stock < item.Quantity)
                            return ApiResponse<bool>.Fail(
                                $"Insufficient stock for {product.Name}");

                        product.Stock -= item.Quantity;

                        if (product.Stock == 0)
                            product.Status = ProductStatus.OutOfStock;

                        await _unitOfWork.Products.UpdateAsync(product);
                    }
                }
            }

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            // ✅ قيد محاسبي تلقائي عند دفع الطلب
            if (status == PaymentStatus.Paid && order.TenantId.HasValue)
            {
                var accounting = _serviceProvider.GetRequiredService<IAccountingService>();
                await accounting.CreateOrderPaidEntryAsync(order.Id, order.TenantId.Value);
            }

            return ApiResponse<bool>.Ok(true, "Payment status updated successfully");
        }

        public async Task<ApiResponse<bool>> CancelOrderAsync(Guid id)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<bool>.Fail("Order not found");

            if (order.Status == OrderStatus.Delivered)
                return ApiResponse<bool>.Fail("Cannot cancel delivered order");

            if (order.Status == OrderStatus.Cancelled)
                return ApiResponse<bool>.Fail("Order is already cancelled");

            // Restore stock لو الـ order كان Paid
            if (order.PaymentStatus == PaymentStatus.Paid && order.Items != null)
            {
                var productIds = order.Items.Select(i => i.ProductId).ToHashSet();
                var products = await _unitOfWork.Products
                    .FindAsync(p => productIds.Contains(p.Id));
                var productMap = products.ToDictionary(p => p.Id);

                foreach (var item in order.Items)
                {
                    if (!productMap.TryGetValue(item.ProductId, out var product)) continue;
                    if (!product.TrackInventory) continue;

                    product.Stock += item.Quantity;

                    if (product.Status == ProductStatus.OutOfStock && product.Stock > 0)
                        product.Status = ProductStatus.Active;

                    await _unitOfWork.Products.UpdateAsync(product);
                }
            }

            order.Status = OrderStatus.Cancelled;

            // إنشاء Return Request تلقائي لو الأوردر كان Paid
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                var returnService = _serviceProvider.GetRequiredService<IReturnService>();
                await returnService.CreateFromCancelAsync(order.Id, order.TenantId ?? Guid.Empty);
            }

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Order cancelled successfully");
        }

        private static string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }

        private static OrderResponseDto MapToDto(Order order) => new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            SubTotal = order.SubTotal,
            ShippingCost = order.ShippingCost,
            Discount = order.Discount,
            Tax = order.Tax,
            Total = order.Total,
            Notes = order.Notes,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingCountry = order.ShippingCountry,
            ShippingPhone = order.ShippingPhone,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            PaidAt = order.PaidAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            TenantId = order.TenantId ?? Guid.Empty,
            CreatedAt = order.CreatedAt,
            Items = order.Items?.Select(i => new OrderItemResponseDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductSKU = i.ProductSKU,
                ProductImage = i.ProductImage,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList() ?? new()
        };
    }
}