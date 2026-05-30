using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Pos;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class PosService : IPosService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PosService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ════════════════════════════════════════════════════════════════
        // SESSIONS
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PosSessionResponseDto>> OpenSessionAsync(
            OpenPosSessionDto dto, Guid cashierId)
        {
            // لا يجوز فتح session جديدة لو في واحدة مفتوحة بالفعل لنفس الكاشير
            var existing = await _unitOfWork.PosSessions.FindAsync(s =>
                s.TenantId == dto.TenantId &&
                s.CashierId == cashierId &&
                s.Status == PosSessionStatus.Open);

            if (existing.Any())
                return ApiResponse<PosSessionResponseDto>.Fail(
                    "You already have an open POS session. Please close it first.");

            var cashier = await _unitOfWork.Users.GetByIdAsync(cashierId);
            if (cashier == null)
                return ApiResponse<PosSessionResponseDto>.Fail("Cashier not found.");

            var session = new PosSession
            {
                TenantId = dto.TenantId,
                CashierId = cashierId,
                TerminalName = dto.TerminalName,
                OpeningCash = dto.OpeningCash,
                Status = PosSessionStatus.Open,
                OpenedAt = DateTime.UtcNow
            };

            await _unitOfWork.PosSessions.AddAsync(session);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<PosSessionResponseDto>.Ok(
                MapSessionToDto(session, cashier),
                "POS session opened successfully.");
        }

        public async Task<ApiResponse<PosSessionSummaryDto>> CloseSessionAsync(
            Guid sessionId, ClosePosSessionDto dto, Guid cashierId)
        {
            var session = await _unitOfWork.PosSessions.GetByIdAsync(sessionId);
            if (session == null)
                return ApiResponse<PosSessionSummaryDto>.Fail("Session not found.");

            if (session.CashierId != cashierId)
                return ApiResponse<PosSessionSummaryDto>.Fail("You can only close your own session.");

            if (session.Status != PosSessionStatus.Open)
                return ApiResponse<PosSessionSummaryDto>.Fail("Session is not open.");

            // اجلب كل أوردرات الجلسة
            var orders = (await _unitOfWork.PosOrders.FindAsync(o =>
                o.PosSessionId == sessionId)).ToList();

            var completedOrders = orders.Where(o => o.Status == PosOrderStatus.Completed).ToList();
            var voidedOrders = orders.Where(o => o.Status == PosOrderStatus.Voided).ToList();
            var refundedOrders = orders.Where(o => o.Status == PosOrderStatus.Refunded).ToList();

            decimal totalSales = completedOrders.Sum(o => o.Total);
            decimal totalCash = completedOrders.Where(o =>
                                         o.PaymentMethod == PosPaymentMethod.Cash ||
                                         o.PaymentMethod == PosPaymentMethod.Mixed)
                                         .Sum(o => o.CashPaid);
            decimal totalCard = completedOrders.Sum(o => o.CardPaid);
            decimal totalRefunds = refundedOrders.Sum(o => o.Total);
            decimal expectedCash = session.OpeningCash + totalCash - totalRefunds;
            decimal cashDifference = dto.ClosingCash - expectedCash;

            // تحديث الـ session
            session.Status = PosSessionStatus.Closed;
            session.ClosedAt = DateTime.UtcNow;
            session.ClosingCash = dto.ClosingCash;
            session.ExpectedCash = expectedCash;
            session.CashDifference = cashDifference;
            session.TotalSales = totalSales;
            session.TotalCashSales = totalCash;
            session.TotalCardSales = totalCard;
            session.TotalRefunds = totalRefunds;
            session.OrdersCount = completedOrders.Count;
            session.Notes = dto.Notes;

            await _unitOfWork.PosSessions.UpdateAsync(session);
            await _unitOfWork.SaveChangesAsync();

            var cashier = await _unitOfWork.Users.GetByIdAsync(cashierId);

            // احسب top products وسيلز by category
            var allItems = completedOrders.SelectMany(o => o.Items ?? []).ToList();

            var topProducts = allItems
                .GroupBy(i => new { i.ProductId, i.ProductName, i.ProductSKU })
                .Select(g => new PosTopProductDto
                {
                    ProductName = g.Key.ProductName,
                    SKU = g.Key.ProductSKU,
                    QuantitySold = g.Sum(i => i.Quantity),
                    TotalSales = g.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(10)
                .ToList();

            var summary = new PosSessionSummaryDto
            {
                SessionId = session.Id,
                TerminalName = session.TerminalName,
                CashierName = cashier != null
                                     ? $"{cashier.FirstName} {cashier.LastName}".Trim()
                                     : string.Empty,
                OpenedAt = session.OpenedAt,
                ClosedAt = session.ClosedAt!.Value,
                OpeningCash = session.OpeningCash,
                ClosingCash = dto.ClosingCash,
                ExpectedCash = expectedCash,
                CashDifference = cashDifference,
                TotalSales = totalSales,
                TotalCashSales = totalCash,
                TotalCardSales = totalCard,
                TotalRefunds = totalRefunds,
                TotalOrders = completedOrders.Count,
                VoidedOrders = voidedOrders.Count,
                TopProducts = topProducts
            };

            return ApiResponse<PosSessionSummaryDto>.Ok(summary, "Session closed successfully.");
        }

        public async Task<ApiResponse<PosSessionResponseDto>> GetActiveSessionAsync(
            Guid tenantId, Guid cashierId)
        {
            var sessions = await _unitOfWork.PosSessions.FindAsync(s =>
                s.TenantId == tenantId &&
                s.CashierId == cashierId &&
                s.Status == PosSessionStatus.Open);

            var session = sessions.FirstOrDefault();
            if (session == null)
                return ApiResponse<PosSessionResponseDto>.Fail("No active session found.");

            var cashier = await _unitOfWork.Users.GetByIdAsync(cashierId);
            return ApiResponse<PosSessionResponseDto>.Ok(MapSessionToDto(session, cashier));
        }

        public async Task<ApiResponse<PagedResponse<PosSessionResponseDto>>> GetSessionsAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.PosSessions.GetPagedAsync(
                s => s.TenantId == tenantId,
                pagination.Skip,
                pagination.PageSize);

            // جيب الكاشيرز دفعة واحدة بدل N+1
            var cashierIds = items.Select(s => s.CashierId).Distinct().ToHashSet();
            var cashiers = (await _unitOfWork.Users.FindAsync(u => cashierIds.Contains(u.Id)))
                                .ToDictionary(u => u.Id);

            var dtos = items
                .OrderByDescending(s => s.OpenedAt)
                .Select(s => MapSessionToDto(s, cashiers.GetValueOrDefault(s.CashierId)))
                .ToList();

            return ApiResponse<PagedResponse<PosSessionResponseDto>>.Ok(
                PagedResponse<PosSessionResponseDto>.Create(dtos, total, pagination));
        }

        public async Task<ApiResponse<PosSessionResponseDto>> GetSessionByIdAsync(Guid sessionId)
        {
            var session = await _unitOfWork.PosSessions.GetByIdAsync(sessionId);
            if (session == null)
                return ApiResponse<PosSessionResponseDto>.Fail("Session not found.");

            var cashier = await _unitOfWork.Users.GetByIdAsync(session.CashierId);
            return ApiResponse<PosSessionResponseDto>.Ok(MapSessionToDto(session, cashier));
        }

        // ════════════════════════════════════════════════════════════════
        // ORDERS / SALES
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PosOrderResponseDto>> CreateOrderAsync(
            CreatePosOrderDto dto, Guid cashierId)
        {
            // تحقق من الـ session
            var session = await _unitOfWork.PosSessions.GetByIdAsync(dto.PosSessionId);
            if (session == null)
                return ApiResponse<PosOrderResponseDto>.Fail("POS session not found.");
            if (session.Status != PosSessionStatus.Open)
                return ApiResponse<PosOrderResponseDto>.Fail("POS session is not open.");
            if (session.CashierId != cashierId)
                return ApiResponse<PosOrderResponseDto>.Fail("Session does not belong to you.");
            if (!dto.Items.Any())
                return ApiResponse<PosOrderResponseDto>.Fail("Order must have at least one item.");

            // جيب المنتجات دفعة واحدة
            var productIds = dto.Items.Select(i => i.ProductId).ToHashSet();
            var allProducts = (await _unitOfWork.Products.FindAsync(p =>
                productIds.Contains(p.Id) && p.TenantId == dto.TenantId))
                .ToDictionary(p => p.Id);

            decimal subTotal = 0;
            var orderItems = new List<PosOrderItem>();

            foreach (var item in dto.Items)
            {
                if (!allProducts.TryGetValue(item.ProductId, out var product))
                    return ApiResponse<PosOrderResponseDto>.Fail(
                        $"Product {item.ProductId} not found.");

                if (product.TrackInventory && product.Stock < item.Quantity)
                    return ApiResponse<PosOrderResponseDto>.Fail(
                        $"Insufficient stock for '{product.Name}'. Available: {product.Stock}.");

                var unitPrice = item.OverridePrice ?? product.Price;
                var lineTotal = (unitPrice * item.Quantity) - item.LineDiscount;
                subTotal += lineTotal;

                orderItems.Add(new PosOrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSKU = product.SKU,
                    ProductBarcode = product.Barcode,
                    ProductImage = product.Images?.FirstOrDefault(i => i.IsMain)?.Url ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    LineDiscount = item.LineDiscount,
                    TotalPrice = lineTotal
                });
            }

            // كوبون خصم
            decimal couponDiscount = 0;
            if (!string.IsNullOrWhiteSpace(dto.CouponCode))
            {
                var coupons = await _unitOfWork.Coupons.FindAsync(c =>
                    c.Code == dto.CouponCode &&
                    c.TenantId == dto.TenantId &&
                    c.IsActive);
                var coupon = coupons.FirstOrDefault();
                if (coupon != null)
                {
                    couponDiscount = coupon.Type == CouponType.Percentage
                        ? subTotal * (coupon.Value / 100)
                        : coupon.Value;
                }
            }

            var totalDiscount = dto.DiscountAmount + couponDiscount;
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(dto.TenantId);
            var taxRate = tenant?.VatRate ?? 0;
            var taxableAmount = subTotal - totalDiscount;
            var taxAmount = Math.Round(taxableAmount * (taxRate / 100), 2);
            var total = taxableAmount + taxAmount;

            // حساب الباقي (Change) للنقدي فقط
            decimal change = 0;
            decimal cashPaid = dto.CashTendered;
            decimal cardPaid = dto.CardPaid;

            if (dto.PaymentMethod == PosPaymentMethod.Cash)
            {
                if (dto.CashTendered < total)
                    return ApiResponse<PosOrderResponseDto>.Fail(
                        $"Cash tendered ({dto.CashTendered}) is less than total ({total}).");
                change = dto.CashTendered - total;
                cashPaid = total;
            }
            else if (dto.PaymentMethod == PosPaymentMethod.Mixed)
            {
                if (cashPaid + cardPaid < total)
                    return ApiResponse<PosOrderResponseDto>.Fail("Total payment is less than order total.");
                change = (cashPaid + cardPaid) - total;
            }

            var posOrder = new PosOrder
            {
                TenantId = dto.TenantId,
                PosSessionId = dto.PosSessionId,
                ReceiptNumber = await GenerateReceiptNumber(dto.TenantId),
                Status = PosOrderStatus.Completed,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                SubTotal = subTotal,
                DiscountAmount = totalDiscount,
                TaxAmount = taxAmount,
                Total = total,
                CashPaid = cashPaid,
                CardPaid = cardPaid,
                Change = change,
                PaymentMethod = dto.PaymentMethod,
                CouponCode = dto.CouponCode,
                Notes = dto.Notes,
                CompletedAt = DateTime.UtcNow,
                Items = orderItems
            };

            await _unitOfWork.PosOrders.AddAsync(posOrder);

            // خصم الستوك فوراً
            foreach (var item in dto.Items)
            {
                var product = allProducts[item.ProductId];
                if (!product.TrackInventory) continue;

                product.Stock -= item.Quantity;
                if (product.Stock <= 0)
                {
                    product.Stock = 0;
                    product.Status = ProductStatus.OutOfStock;
                }
                await _unitOfWork.Products.UpdateAsync(product);
            }

            await _unitOfWork.SaveChangesAsync();

            var cashier = await _unitOfWork.Users.GetByIdAsync(cashierId);
            return ApiResponse<PosOrderResponseDto>.Ok(
                await MapOrderToDto(posOrder, session, cashier, tenant),
                "Sale completed successfully.");
        }

        public async Task<ApiResponse<bool>> VoidOrderAsync(
            Guid orderId, VoidPosOrderDto dto, Guid cashierId)
        {
            var order = await _unitOfWork.PosOrders.GetByIdAsync(orderId);
            if (order == null)
                return ApiResponse<bool>.Fail("POS order not found.");

            if (order.Status == PosOrderStatus.Voided)
                return ApiResponse<bool>.Fail("Order is already voided.");

            if (order.Status != PosOrderStatus.Completed &&
                order.Status != PosOrderStatus.Draft)
                return ApiResponse<bool>.Fail("Only Completed or Draft orders can be voided.");

            // تحقق إن الكاشير يملك الـ session
            var session = await _unitOfWork.PosSessions.GetByIdAsync(order.PosSessionId);
            if (session?.CashierId != cashierId)
                return ApiResponse<bool>.Fail("You can only void orders from your own session.");

            // أعد الستوك
            if (order.Items != null && order.Status == PosOrderStatus.Completed)
            {
                var productIds = order.Items.Select(i => i.ProductId).ToHashSet();
                var products = (await _unitOfWork.Products.FindAsync(p =>
                    productIds.Contains(p.Id)))
                    .ToDictionary(p => p.Id);

                foreach (var item in order.Items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product)) continue;
                    if (!product.TrackInventory) continue;

                    product.Stock += item.Quantity;
                    if (product.Status == ProductStatus.OutOfStock && product.Stock > 0)
                        product.Status = ProductStatus.Active;

                    await _unitOfWork.Products.UpdateAsync(product);
                }
            }

            order.Status = PosOrderStatus.Voided;
            order.Notes = string.IsNullOrWhiteSpace(dto.Reason)
                ? order.Notes
                : $"{order.Notes} | VOID: {dto.Reason}".Trim(' ', '|', ' ');

            await _unitOfWork.PosOrders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Order voided successfully.");
        }

        public async Task<ApiResponse<PosOrderResponseDto>> GetOrderReceiptAsync(Guid orderId)
        {
            var order = await _unitOfWork.PosOrders.GetByIdAsync(orderId);
            if (order == null)
                return ApiResponse<PosOrderResponseDto>.Fail("POS order not found.");

            var session = await _unitOfWork.PosSessions.GetByIdAsync(order.PosSessionId);
            var cashier = session != null
                ? await _unitOfWork.Users.GetByIdAsync(session.CashierId)
                : null;
            var tenant = order.TenantId.HasValue
                ? await _unitOfWork.Tenants.GetByIdAsync(order.TenantId.Value)
                : null;

            return ApiResponse<PosOrderResponseDto>.Ok(
                await MapOrderToDto(order, session, cashier, tenant));
        }

        public async Task<ApiResponse<List<PosOrderResponseDto>>> GetSessionOrdersAsync(Guid sessionId)
        {
            var session = await _unitOfWork.PosSessions.GetByIdAsync(sessionId);
            if (session == null)
                return ApiResponse<List<PosOrderResponseDto>>.Fail("Session not found.");

            var orders = (await _unitOfWork.PosOrders.FindAsync(o =>
                o.PosSessionId == sessionId))
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var cashier = await _unitOfWork.Users.GetByIdAsync(session.CashierId);
            var tenant = session.TenantId.HasValue
                ? await _unitOfWork.Tenants.GetByIdAsync(session.TenantId.Value)
                : null;

            var dtos = new List<PosOrderResponseDto>();
            foreach (var o in orders)
                dtos.Add(await MapOrderToDto(o, session, cashier, tenant));

            return ApiResponse<List<PosOrderResponseDto>>.Ok(dtos);
        }

        // ════════════════════════════════════════════════════════════════
        // PRODUCTS (Quick Search)
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<List<PosProductDto>>> SearchProductsAsync(
            Guid tenantId, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ApiResponse<List<PosProductDto>>.Ok(new());

            query = query.Trim().ToLower();

            var products = await _unitOfWork.Products.FindAsync(p =>
                p.TenantId == tenantId &&
                p.IsActive &&
                p.Status != ProductStatus.Deleted &&
                (p.Name.ToLower().Contains(query) ||
                 p.SKU.ToLower().Contains(query) ||
                 p.Barcode.ToLower().Contains(query)));

            var dtos = products
                .Take(20)   // حد أقصى للبحث السريع
                .Select(MapProductToDto)
                .ToList();

            return ApiResponse<List<PosProductDto>>.Ok(dtos);
        }

        public async Task<ApiResponse<PosProductDto>> GetProductByBarcodeAsync(
            Guid tenantId, string barcode)
        {
            var products = await _unitOfWork.Products.FindAsync(p =>
                p.TenantId == tenantId &&
                p.IsActive &&
                p.Barcode == barcode);

            var product = products.FirstOrDefault();
            if (product == null)
                return ApiResponse<PosProductDto>.Fail($"No product found with barcode '{barcode}'.");

            return ApiResponse<PosProductDto>.Ok(MapProductToDto(product));
        }

        // ════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════

        private static PosSessionResponseDto MapSessionToDto(PosSession session, User? cashier) => new()
        {
            Id = session.Id,
            TenantId = session.TenantId ?? Guid.Empty,
            CashierId = session.CashierId,
            CashierName = cashier != null
                                 ? $"{cashier.FirstName} {cashier.LastName}".Trim()
                                 : string.Empty,
            TerminalName = session.TerminalName,
            Status = session.Status,
            StatusLabel = session.Status.ToString(),
            OpeningCash = session.OpeningCash,
            ClosingCash = session.ClosingCash,
            ExpectedCash = session.ExpectedCash,
            CashDifference = session.CashDifference,
            OpenedAt = session.OpenedAt,
            ClosedAt = session.ClosedAt,
            TotalSales = session.TotalSales,
            TotalCashSales = session.TotalCashSales,
            TotalCardSales = session.TotalCardSales,
            TotalRefunds = session.TotalRefunds,
            OrdersCount = session.OrdersCount,
            Notes = session.Notes
        };

        private static Task<PosOrderResponseDto> MapOrderToDto(
            PosOrder order,
            PosSession? session,
            User? cashier,
            Tenant? tenant)
        {
            var dto = new PosOrderResponseDto
            {
                Id = order.Id,
                ReceiptNumber = order.ReceiptNumber,
                PosSessionId = order.PosSessionId,
                TerminalName = session?.TerminalName ?? string.Empty,
                CashierName = cashier != null
                                        ? $"{cashier.FirstName} {cashier.LastName}".Trim()
                                        : string.Empty,
                Status = order.Status,
                StatusLabel = order.Status.ToString(),
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                TaxAmount = order.TaxAmount,
                Total = order.Total,
                CashPaid = order.CashPaid,
                CardPaid = order.CardPaid,
                Change = order.Change,
                PaymentMethod = order.PaymentMethod,
                PaymentMethodLabel = order.PaymentMethod.ToString(),
                CouponCode = order.CouponCode,
                Notes = order.Notes,
                CompletedAt = order.CompletedAt,
                CreatedAt = order.CreatedAt,
                // بيانات الطباعة
                TenantName = tenant?.Name ?? string.Empty,
                TenantLogo = tenant?.Logo ?? string.Empty,
                TenantPhone = tenant?.Phone ?? string.Empty,
                TenantAddress = string.Empty,
                TenantVatNumber = tenant?.VatNumber ?? string.Empty,
                Items = order.Items?.Select(i => new PosOrderItemResponseDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    ProductSKU = i.ProductSKU,
                    ProductBarcode = i.ProductBarcode,
                    ProductImage = i.ProductImage,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineDiscount = i.LineDiscount,
                    TotalPrice = i.TotalPrice
                }).ToList() ?? new()
            };

            return Task.FromResult(dto);
        }

        private static PosProductDto MapProductToDto(Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            SKU = p.SKU,
            Barcode = p.Barcode,
            Image = p.Images?.FirstOrDefault(i => i.IsMain)?.Url ?? string.Empty,
            Price = p.Price,
            Stock = p.Stock,
            TrackInventory = p.TrackInventory
        };

        private async Task<string> GenerateReceiptNumber(Guid tenantId)
        {
            var todayPrefix = $"POS-{DateTime.UtcNow:yyyyMMdd}";
            var todayOrders = await _unitOfWork.PosOrders.FindAsync(o =>
                o.TenantId == tenantId &&
                o.ReceiptNumber.StartsWith(todayPrefix));

            var seq = todayOrders.Count() + 1;
            return $"{todayPrefix}-{seq:D4}";
        }
    }
}