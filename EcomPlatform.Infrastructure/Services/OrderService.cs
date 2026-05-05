using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Orders;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<OrderResponseDto>> CreateAsync(CreateOrderDto dto)
        {
            // Calculate totals
            decimal subTotal = 0;
            var orderItems = new List<OrderItem>();

            foreach (var item in dto.Items)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                if (product == null)
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

                // Update stock
                if (product.TrackInventory)
                {
                    product.Stock -= item.Quantity;
                    if (product.Stock == 0)
                        product.Status = ProductStatus.OutOfStock;
                    await _unitOfWork.Products.UpdateAsync(product);
                }
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

            return ApiResponse<OrderResponseDto>.Ok(MapToDto(order), "Order created successfully");
        }

        public async Task<ApiResponse<OrderResponseDto>> GetByIdAsync(Guid id)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<OrderResponseDto>.Fail("Order not found");

            return ApiResponse<OrderResponseDto>.Ok(MapToDto(order));
        }

        public async Task<ApiResponse<IEnumerable<OrderResponseDto>>> GetAllByTenantAsync(Guid tenantId)
        {
            var orders = await _unitOfWork.Orders.FindAsync(o => o.TenantId == tenantId);
            var result = orders.Select(MapToDto);
            return ApiResponse<IEnumerable<OrderResponseDto>>.Ok(result);
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

            return ApiResponse<bool>.Ok(true, "Order status updated successfully");
        }

        public async Task<ApiResponse<bool>> UpdatePaymentStatusAsync(Guid id, PaymentStatus status)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<bool>.Fail("Order not found");

            order.PaymentStatus = status;

            if (status == PaymentStatus.Paid)
                order.PaidAt = DateTime.UtcNow;

            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Payment status updated successfully");
        }

        public async Task<ApiResponse<bool>> CancelOrderAsync(Guid id)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return ApiResponse<bool>.Fail("Order not found");

            if (order.Status == OrderStatus.Delivered)
                return ApiResponse<bool>.Fail("Cannot cancel delivered order");

            order.Status = OrderStatus.Cancelled;
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
            TenantId = order.TenantId,
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