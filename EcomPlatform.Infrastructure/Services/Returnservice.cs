using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Notifications;
using EcomPlatform.Application.DTOs.Returns;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class ReturnService : IReturnService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;
        private readonly IAccountingService _accountingService;

        public ReturnService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IEmailService emailService,
            IAuditLogService auditLogService,
            IAccountingService accountingService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _accountingService = accountingService;
        }

        // ════════════════════════════════════════════════════════════════════
        // CREATE
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ReturnRequestResponseDto>> CreateAsync(CreateReturnRequestDto dto)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId);
            if (order == null)
                return ApiResponse<ReturnRequestResponseDto>.Fail("Order not found");

            // الأوردر لازم يكون Delivered أو Shipped أو Cancelled عشان يتعمله return
            var allowedStatuses = new[] { OrderStatus.Delivered, OrderStatus.Shipped, OrderStatus.Cancelled };
            if (!allowedStatuses.Contains(order.Status))
                return ApiResponse<ReturnRequestResponseDto>.Fail(
                    $"Cannot create return for order with status '{order.Status}'");

            // مفيش return request pending موجود على نفس الأوردر
            var existing = await _unitOfWork.ReturnRequests.FindAsync(
                r => r.OrderId == dto.OrderId &&
                     r.Status != ReturnStatus.Rejected &&
                     r.Status != ReturnStatus.Cancelled);
            if (existing.Any())
                return ApiResponse<ReturnRequestResponseDto>.Fail("A return request already exists for this order");

            // تحقق من الـ items
            var orderItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == dto.OrderId);
            var orderItemMap = orderItems.ToDictionary(i => i.Id);

            var returnItems = new List<ReturnItem>();
            decimal requestedAmount = 0;

            foreach (var itemDto in dto.Items)
            {
                if (!orderItemMap.TryGetValue(itemDto.OrderItemId, out var orderItem))
                    return ApiResponse<ReturnRequestResponseDto>.Fail(
                        $"OrderItem {itemDto.OrderItemId} not found in this order");

                if (itemDto.QuantityRequested > orderItem.Quantity)
                    return ApiResponse<ReturnRequestResponseDto>.Fail(
                        $"Requested quantity ({itemDto.QuantityRequested}) exceeds ordered quantity ({orderItem.Quantity}) for '{orderItem.ProductName}'");

                returnItems.Add(new ReturnItem
                {
                    OrderItemId = orderItem.Id,
                    ProductId = orderItem.ProductId,
                    ProductName = orderItem.ProductName,
                    ProductSKU = orderItem.ProductSKU,
                    QuantityRequested = itemDto.QuantityRequested,
                    QuantityApproved = 0,
                    UnitPrice = orderItem.UnitPrice
                });

                requestedAmount += orderItem.UnitPrice * itemDto.QuantityRequested;
            }

            var returnRequest = new ReturnRequest
            {
                ReturnNumber = await GenerateReturnNumberAsync(),
                OrderId = dto.OrderId,
                Initiator = dto.Initiator,
                Reason = dto.Reason,
                ReasonNote = dto.ReasonNote,
                Status = ReturnStatus.Pending,
                RequestedAmount = requestedAmount,
                ApprovedAmount = 0,
                RefundStatus = order.PaymentStatus == PaymentStatus.Paid
                                    ? RefundStatus.Pending
                                    : RefundStatus.Skipped,
                TenantId = dto.TenantId
            };

            await _unitOfWork.ReturnRequests.AddAsync(returnRequest);

            foreach (var item in returnItems)
            {
                item.ReturnRequestId = returnRequest.Id;
                await _unitOfWork.ReturnItems.AddAsync(item);
            }

            await _unitOfWork.SaveChangesAsync();

            // إشعار الـ admins
            await NotifyAdminsAsync(returnRequest, order, "طلب إرجاع جديد");

            await _auditLogService.LogAsync(
                entityName: "ReturnRequest",
                entityId: returnRequest.Id.ToString(),
                action: AuditAction.Create,
                userId: Guid.Empty,
                tenantId: dto.TenantId,
                newValue: $"Return '{returnRequest.ReturnNumber}' for Order '{order.OrderNumber}' — Amount: {requestedAmount:N2}");

            returnRequest.Items = returnItems;
            returnRequest.Order = order;
            return ApiResponse<ReturnRequestResponseDto>.Ok(MapToResponse(returnRequest), "Return request created successfully");
        }

        // ── تلقائي من Cancel ──────────────────────────────────────────────

        public async Task<ApiResponse<ReturnRequestResponseDto>> CreateFromCancelAsync(Guid orderId, Guid tenantId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return ApiResponse<ReturnRequestResponseDto>.Fail("Order not found");

            // لو مش Paid مفيش لازم return
            if (order.PaymentStatus != PaymentStatus.Paid)
                return ApiResponse<ReturnRequestResponseDto>.Fail("Order was not paid — no refund needed");

            var orderItems = await _unitOfWork.OrderItems.FindAsync(i => i.OrderId == orderId);
            if (!orderItems.Any())
                return ApiResponse<ReturnRequestResponseDto>.Fail("Order has no items");

            var dto = new CreateReturnRequestDto
            {
                OrderId = orderId,
                Reason = ReturnReason.OrderCancelled,
                ReasonNote = "تم إنشاء طلب الإرجاع تلقائياً عند إلغاء الطلب",
                Initiator = ReturnInitiator.System,
                TenantId = tenantId,
                Items = orderItems.Select(i => new CreateReturnItemDto
                {
                    OrderItemId = i.Id,
                    QuantityRequested = i.Quantity
                }).ToList()
            };

            return await CreateAsync(dto);
        }

        // ════════════════════════════════════════════════════════════════════
        // READ
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ReturnRequestResponseDto>> GetByIdAsync(Guid id)
        {
            var r = await _unitOfWork.ReturnRequests.GetByIdAsync(id);
            if (r == null) return ApiResponse<ReturnRequestResponseDto>.Fail("Return request not found");
            await LoadNavigationsAsync(r);
            return ApiResponse<ReturnRequestResponseDto>.Ok(MapToResponse(r));
        }

        public async Task<ApiResponse<ReturnRequestResponseDto>> GetByReturnNumberAsync(string returnNumber)
        {
            var results = await _unitOfWork.ReturnRequests.FindAsync(
                r => r.ReturnNumber == returnNumber.ToUpper());
            var r = results.FirstOrDefault();
            if (r == null) return ApiResponse<ReturnRequestResponseDto>.Fail("Return request not found");
            await LoadNavigationsAsync(r);
            return ApiResponse<ReturnRequestResponseDto>.Ok(MapToResponse(r));
        }

        public async Task<ApiResponse<PagedResponse<ReturnRequestResponseDto>>> GetByOrderAsync(Guid orderId)
        {
            var items = await _unitOfWork.ReturnRequests.FindAsync(r => r.OrderId == orderId);
            foreach (var r in items) await LoadNavigationsAsync(r);
            var dtos = items.OrderByDescending(r => r.CreatedAt).Select(MapToResponse).ToList();
            return ApiResponse<PagedResponse<ReturnRequestResponseDto>>.Ok(
                PagedResponse<ReturnRequestResponseDto>.Create(dtos, dtos.Count, new PaginationParams()));
        }

        public async Task<ApiResponse<PagedResponse<ReturnRequestResponseDto>>> GetByTenantAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.ReturnRequests.GetPagedAsync(
                r => r.TenantId == tenantId, pagination.Skip, pagination.PageSize);
            foreach (var r in items) await LoadNavigationsAsync(r);
            return ApiResponse<PagedResponse<ReturnRequestResponseDto>>.Ok(
                PagedResponse<ReturnRequestResponseDto>.Create(
                    items.Select(MapToResponse).ToList(), total, pagination));
        }

        // ════════════════════════════════════════════════════════════════════
        // REVIEW
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ReturnRequestResponseDto>> ReviewAsync(Guid id, ReviewReturnRequestDto dto)
        {
            var returnRequest = await _unitOfWork.ReturnRequests.GetByIdAsync(id);
            if (returnRequest == null)
                return ApiResponse<ReturnRequestResponseDto>.Fail("Return request not found");

            if (returnRequest.Status != ReturnStatus.Pending)
                return ApiResponse<ReturnRequestResponseDto>.Fail(
                    $"Cannot review a return in status '{returnRequest.Status}'");

            returnRequest.ReviewedById = dto.ReviewedById;
            returnRequest.ReviewedAt = DateTime.UtcNow;
            returnRequest.UpdatedAt = DateTime.UtcNow;

            if (!dto.Approved)
            {
                returnRequest.Status = ReturnStatus.Rejected;
                returnRequest.RefundStatus = RefundStatus.Skipped;
                returnRequest.RefundNote = dto.Note;

                await _unitOfWork.ReturnRequests.UpdateAsync(returnRequest);
                await _unitOfWork.SaveChangesAsync();

                await _auditLogService.LogAsync("ReturnRequest", id.ToString(), AuditAction.Update,
                    dto.ReviewedById, returnRequest.TenantId,
                    oldValue: "Pending", newValue: "Rejected");

                await LoadNavigationsAsync(returnRequest);
                return ApiResponse<ReturnRequestResponseDto>.Ok(MapToResponse(returnRequest), "Return request rejected");
            }

            // ── Approved ─────────────────────────────────────────────────

            var returnItems = (await _unitOfWork.ReturnItems.FindAsync(i => i.ReturnRequestId == id)).ToList();
            var approvedMap = dto.ApprovedItems.ToDictionary(x => x.ReturnItemId, x => x.QuantityApproved);

            decimal approvedAmount = 0;
            foreach (var item in returnItems)
            {
                int qty = approvedMap.TryGetValue(item.Id, out var approved) ? approved : item.QuantityRequested;
                item.QuantityApproved = qty;
                approvedAmount += item.UnitPrice * qty;
                await _unitOfWork.ReturnItems.UpdateAsync(item);
            }

            returnRequest.Status = ReturnStatus.Approved;
            returnRequest.ApprovedAmount = approvedAmount;
            returnRequest.RefundNote = dto.Note;

            // ── إرجاع المخزون تلقائياً ────────────────────────────────────
            await RestoreStockAsync(returnItems, returnRequest);

            await _unitOfWork.ReturnRequests.UpdateAsync(returnRequest);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("ReturnRequest", id.ToString(), AuditAction.Update,
                dto.ReviewedById, returnRequest.TenantId,
                oldValue: "Pending", newValue: $"Approved — Amount: {approvedAmount:N2}");

            // إشعار العميل
            var order = await _unitOfWork.Orders.GetByIdAsync(returnRequest.OrderId);
            if (order != null)
                await NotifyCustomerAsync(order, returnRequest, "تمت الموافقة على طلب إرجاعك");

            returnRequest.Items = returnItems;
            returnRequest.Order = order;
            return ApiResponse<ReturnRequestResponseDto>.Ok(MapToResponse(returnRequest), "Return request approved");
        }

        // ════════════════════════════════════════════════════════════════════
        // PROCESS REFUND
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<bool>> ProcessRefundAsync(ProcessRefundDto dto)
        {
            var returnRequest = await _unitOfWork.ReturnRequests.GetByIdAsync(dto.ReturnRequestId);
            if (returnRequest == null)
                return ApiResponse<bool>.Fail("Return request not found");

            if (returnRequest.Status != ReturnStatus.Approved)
                return ApiResponse<bool>.Fail("Return request must be Approved before processing refund");

            if (returnRequest.RefundStatus == RefundStatus.Completed)
                return ApiResponse<bool>.Fail("Refund already completed");

            if (returnRequest.RefundStatus == RefundStatus.Skipped)
                return ApiResponse<bool>.Fail("Refund was skipped — order was not paid");

            returnRequest.RefundStatus = RefundStatus.Completed;
            returnRequest.RefundMethod = dto.Method;
            returnRequest.RefundedAt = DateTime.UtcNow;
            returnRequest.RefundGatewayTransactionId = dto.GatewayTransactionId;
            returnRequest.RefundNote = dto.Note;
            returnRequest.Status = ReturnStatus.Completed;
            returnRequest.UpdatedAt = DateTime.UtcNow;

            // تحديث PaymentStatus للأوردر
            var order = await _unitOfWork.Orders.GetByIdAsync(returnRequest.OrderId);
            if (order != null)
            {
                order.PaymentStatus = PaymentStatus.Refunded;
                order.Status = OrderStatus.Refunded;
                order.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Orders.UpdateAsync(order);
            }

            await _unitOfWork.ReturnRequests.UpdateAsync(returnRequest);
            await _unitOfWork.SaveChangesAsync();

            // ✅ قيد محاسبي تلقائي عند اعتماد الاسترداد
            if (returnRequest.TenantId.HasValue)
                await _accountingService.CreateRefundEntryAsync(returnRequest.Id, returnRequest.TenantId.Value);

            await _auditLogService.LogAsync("ReturnRequest", dto.ReturnRequestId.ToString(), AuditAction.Update,
                dto.ProcessedById, returnRequest.TenantId,
                oldValue: "Approved", newValue: $"Refunded — Method: {dto.Method}, Amount: {returnRequest.ApprovedAmount:N2}");

            // إشعار العميل
            if (order != null)
                await NotifyCustomerAsync(order, returnRequest, "تم استرداد مبلغك بنجاح");

            return ApiResponse<bool>.Ok(true, "Refund processed successfully");
        }

        // ════════════════════════════════════════════════════════════════════
        // CANCEL BY CUSTOMER
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<bool>> CancelByCustomerAsync(Guid id)
        {
            var returnRequest = await _unitOfWork.ReturnRequests.GetByIdAsync(id);
            if (returnRequest == null)
                return ApiResponse<bool>.Fail("Return request not found");

            if (returnRequest.Status != ReturnStatus.Pending)
                return ApiResponse<bool>.Fail("Can only cancel a pending return request");

            returnRequest.Status = ReturnStatus.Cancelled;
            returnRequest.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.ReturnRequests.UpdateAsync(returnRequest);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Return request cancelled");
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE — Stock Restore
        // ════════════════════════════════════════════════════════════════════

        private async Task RestoreStockAsync(List<ReturnItem> items, ReturnRequest returnRequest)
        {
            if (returnRequest.StockRestored) return;

            var productIds = items.Select(i => i.ProductId).ToHashSet();
            var products = await _unitOfWork.Products.FindAsync(p => productIds.Contains(p.Id));
            var productMap = products.ToDictionary(p => p.Id);

            foreach (var item in items)
            {
                if (item.QuantityApproved <= 0) continue;
                if (!productMap.TryGetValue(item.ProductId, out var product)) continue;
                if (!product.TrackInventory) continue;

                product.Stock += item.QuantityApproved;

                if (product.Status == ProductStatus.OutOfStock && product.Stock > 0)
                    product.Status = ProductStatus.Active;

                await _unitOfWork.Products.UpdateAsync(product);
            }

            returnRequest.StockRestored = true;
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE — Notifications
        // ════════════════════════════════════════════════════════════════════

        private async Task NotifyAdminsAsync(ReturnRequest returnRequest, Order order, string title)
        {
            try
            {
                var admins = await _unitOfWork.Users.FindAsync(
                    u => u.TenantId == returnRequest.TenantId && u.Role == UserRole.TenantAdmin);

                foreach (var admin in admins)
                {
                    await _notificationService.CreateAsync(new CreateNotificationDto
                    {
                        UserId = admin.Id,
                        Title = title,
                        Message = $"طلب إرجاع جديد #{returnRequest.ReturnNumber} على الطلب #{order.OrderNumber}",
                        Type = NotificationType.Return
                    });

                    _ = _emailService.SendAsync(admin.Email, title,
                        BuildAdminNotificationEmail(returnRequest, order));
                }
            }
            catch { }
        }

        private async Task NotifyCustomerAsync(Order order, ReturnRequest returnRequest, string title)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(order.CustomerEmail)) return;

                _ = _emailService.SendAsync(order.CustomerEmail, title,
                    BuildCustomerNotificationEmail(order, returnRequest, title));
            }
            catch { }
        }

        private static string BuildAdminNotificationEmail(ReturnRequest r, Order order) => $@"
            <div dir='rtl' style='font-family:Arial;max-width:600px;'>
                <h2>🔄 طلب إرجاع جديد</h2>
                <table style='border-collapse:collapse;width:100%;'>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>رقم الإرجاع</b></td><td style='padding:8px;'>{r.ReturnNumber}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>رقم الطلب</b></td><td style='padding:8px;'>{order.OrderNumber}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>العميل</b></td><td style='padding:8px;'>{order.CustomerName}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>المبلغ</b></td><td style='padding:8px;'>{r.RequestedAmount:N2}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>السبب</b></td><td style='padding:8px;'>{r.Reason}</td></tr>
                </table>
            </div>";

        private static string BuildCustomerNotificationEmail(Order order, ReturnRequest r, string title) => $@"
            <div dir='rtl' style='font-family:Arial;max-width:600px;'>
                <h2>{title}</h2>
                <p>مرحباً {order.CustomerName}</p>
                <table style='border-collapse:collapse;width:100%;'>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>رقم الإرجاع</b></td><td style='padding:8px;'>{r.ReturnNumber}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>رقم الطلب</b></td><td style='padding:8px;'>{order.OrderNumber}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>المبلغ المُسترد</b></td><td style='padding:8px;'>{r.ApprovedAmount:N2}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>الحالة</b></td><td style='padding:8px;'>{r.Status}</td></tr>
                </table>
            </div>";

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE — Helpers
        // ════════════════════════════════════════════════════════════════════

        private async Task<string> GenerateReturnNumberAsync()
        {
            string number;
            bool exists;
            do
            {
                number = $"RET-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
                var found = await _unitOfWork.ReturnRequests.FindAsync(r => r.ReturnNumber == number);
                exists = found.Any();
            } while (exists);
            return number;
        }

        private async Task LoadNavigationsAsync(ReturnRequest r)
        {
            r.Order = await _unitOfWork.Orders.GetByIdAsync(r.OrderId);
            r.Items = (await _unitOfWork.ReturnItems.FindAsync(i => i.ReturnRequestId == r.Id)).ToList();
            if (r.ReviewedById.HasValue)
                r.ReviewedBy = await _unitOfWork.Users.GetByIdAsync(r.ReviewedById.Value);
        }

        private static ReturnRequestResponseDto MapToResponse(ReturnRequest r) => new()
        {
            Id = r.Id,
            ReturnNumber = r.ReturnNumber,
            OrderId = r.OrderId,
            OrderNumber = r.Order?.OrderNumber ?? string.Empty,
            CustomerName = r.Order?.CustomerName ?? string.Empty,
            CustomerEmail = r.Order?.CustomerEmail ?? string.Empty,
            CustomerPhone = r.Order?.CustomerPhone ?? string.Empty,
            Initiator = r.Initiator,
            InitiatorName = r.Initiator.ToString(),
            Reason = r.Reason,
            ReasonName = r.Reason.ToString(),
            ReasonNote = r.ReasonNote,
            Status = r.Status,
            StatusName = r.Status.ToString(),
            RequestedAmount = r.RequestedAmount,
            ApprovedAmount = r.ApprovedAmount,
            RefundStatus = r.RefundStatus,
            RefundStatusName = r.RefundStatus.ToString(),
            RefundMethod = r.RefundMethod,
            RefundMethodName = r.RefundMethod.ToString(),
            RefundedAt = r.RefundedAt,
            RefundNote = r.RefundNote,
            StockRestored = r.StockRestored,
            ReviewedByName = r.ReviewedBy != null
                        ? $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}".Trim()
                        : string.Empty,
            ReviewedAt = r.ReviewedAt,
            TenantId = r.TenantId,
            CreatedAt = r.CreatedAt,
            Items = r.Items.Select(i => new ReturnItemResponseDto
            {
                Id = i.Id,
                OrderItemId = i.OrderItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductSKU = i.ProductSKU,
                QuantityRequested = i.QuantityRequested,
                QuantityApproved = i.QuantityApproved,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList()
        };
    }
}