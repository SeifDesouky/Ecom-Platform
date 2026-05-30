using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Notifications;
using EcomPlatform.Application.DTOs.Orders;
using EcomPlatform.Application.DTOs.PaymentLinks;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EcomPlatform.Infrastructure.Services
{
    public class PaymentLinkService : IPaymentLinkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;
        private readonly IOrderService _orderService;
        private readonly IAuditLogService _auditLogService;
        private readonly IConfiguration _configuration;

        public PaymentLinkService(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            INotificationService notificationService,
            IOrderService orderService,
            IAuditLogService auditLogService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _notificationService = notificationService;
            _orderService = orderService;
            _auditLogService = auditLogService;
            _configuration = configuration;
        }

        // ════════════════════════════════════════════════════════════════════
        // CREATE
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PaymentLinkResponseDto>> CreateAsync(CreatePaymentLinkDto dto)
        {
            // Validate OrderBased
            if (dto.LinkType == PaymentLinkType.OrderBased)
            {
                if (!dto.OrderId.HasValue)
                    return ApiResponse<PaymentLinkResponseDto>.Fail("OrderId is required for OrderBased links");

                var order = await _unitOfWork.Orders.GetByIdAsync(dto.OrderId.Value);
                if (order == null)
                    return ApiResponse<PaymentLinkResponseDto>.Fail("Order not found");

                if (order.PaymentStatus == PaymentStatus.Paid)
                    return ApiResponse<PaymentLinkResponseDto>.Fail("Order is already paid");

                dto.Amount = order.Total;
            }

            // Validate ProductBased
            if (dto.LinkType == PaymentLinkType.ProductBased)
            {
                if (!dto.Items.Any())
                    return ApiResponse<PaymentLinkResponseDto>.Fail("At least one product is required for ProductBased links");
            }

            // Validate expiry
            if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value <= DateTime.UtcNow)
                return ApiResponse<PaymentLinkResponseDto>.Fail("ExpiresAt must be a future date");

            var link = new PaymentLink
            {
                Code = await GenerateUniqueCodeAsync(),
                Title = dto.Title,
                Description = dto.Description,
                LinkType = dto.LinkType,
                Amount = dto.Amount,
                Currency = dto.Currency,
                OrderId = dto.OrderId,
                ExpiresAt = dto.ExpiresAt,
                MaxUses = dto.MaxUses,
                SuccessRedirectUrl = dto.SuccessRedirectUrl,
                FailureRedirectUrl = dto.FailureRedirectUrl,
                Metadata = dto.Metadata,
                CreatedById = dto.CreatedById,
                TenantId = dto.TenantId,
                Status = PaymentLinkStatus.Active
            };

            await _unitOfWork.PaymentLinks.AddAsync(link);

            // إضافة المنتجات لو ProductBased
            if (dto.LinkType == PaymentLinkType.ProductBased)
            {
                decimal calculatedAmount = 0;
                foreach (var itemDto in dto.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(itemDto.ProductId);
                    if (product == null)
                        return ApiResponse<PaymentLinkResponseDto>.Fail($"Product {itemDto.ProductId} not found");

                    var item = new PaymentLinkItem
                    {
                        PaymentLinkId = link.Id,
                        ProductId = itemDto.ProductId,
                        Quantity = itemDto.Quantity,
                        UnitPrice = product.Price,
                        ProductName = product.Name
                    };

                    await _unitOfWork.PaymentLinkItems.AddAsync(item);
                    calculatedAmount += item.UnitPrice * item.Quantity;
                }
                link.Amount = calculatedAmount;
            }

            await _unitOfWork.SaveChangesAsync();

            await NotifyLinkCreatedAsync(link);

            await _auditLogService.LogAsync(
                entityName: "PaymentLink",
                entityId: link.Id.ToString(),
                action: AuditAction.Create,
                userId: dto.CreatedById ?? Guid.Empty,
                tenantId: dto.TenantId,
                newValue: $"PaymentLink '{link.Title}' ({link.Code}) created — Amount: {link.Amount} {link.Currency}");

            await LoadNavigationsAsync(link);
            return ApiResponse<PaymentLinkResponseDto>.Ok(MapToResponse(link), "Payment link created successfully");
        }

        // ════════════════════════════════════════════════════════════════════
        // READ
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PaymentLinkResponseDto>> GetByIdAsync(Guid id)
        {
            var link = await _unitOfWork.PaymentLinks.GetByIdAsync(id);
            if (link == null)
                return ApiResponse<PaymentLinkResponseDto>.Fail("Payment link not found");

            await LoadNavigationsAsync(link);
            return ApiResponse<PaymentLinkResponseDto>.Ok(MapToResponse(link));
        }

        public async Task<ApiResponse<PaymentLinkResponseDto>> GetByCodeAsync(string code)
        {
            var links = await _unitOfWork.PaymentLinks.FindAsync(l => l.Code == code.ToUpper());
            var link = links.FirstOrDefault();
            if (link == null)
                return ApiResponse<PaymentLinkResponseDto>.Fail("Payment link not found");

            await LoadNavigationsAsync(link);
            return ApiResponse<PaymentLinkResponseDto>.Ok(MapToResponse(link));
        }

        public async Task<ApiResponse<PagedResponse<PaymentLinkResponseDto>>> GetByTenantAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.PaymentLinks.GetPagedAsync(
                l => l.TenantId == tenantId,
                pagination.Skip, pagination.PageSize);

            foreach (var link in items)
                await LoadNavigationsAsync(link);

            var result = PagedResponse<PaymentLinkResponseDto>.Create(
                items.Select(MapToResponse).ToList(), total, pagination);

            return ApiResponse<PagedResponse<PaymentLinkResponseDto>>.Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // UPDATE
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PaymentLinkResponseDto>> UpdateAsync(Guid id, UpdatePaymentLinkDto dto)
        {
            var link = await _unitOfWork.PaymentLinks.GetByIdAsync(id);
            if (link == null)
                return ApiResponse<PaymentLinkResponseDto>.Fail("Payment link not found");

            if (link.Status == PaymentLinkStatus.Paid)
                return ApiResponse<PaymentLinkResponseDto>.Fail("Cannot update a fully paid link");

            if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value <= DateTime.UtcNow)
                return ApiResponse<PaymentLinkResponseDto>.Fail("ExpiresAt must be a future date");

            link.Title = string.IsNullOrWhiteSpace(dto.Title) ? link.Title : dto.Title;
            link.Description = dto.Description;
            link.ExpiresAt = dto.ExpiresAt;
            link.MaxUses = dto.MaxUses;
            link.SuccessRedirectUrl = dto.SuccessRedirectUrl;
            link.FailureRedirectUrl = dto.FailureRedirectUrl;
            link.Metadata = dto.Metadata;
            link.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.PaymentLinks.UpdateAsync(link);
            await _unitOfWork.SaveChangesAsync();

            await LoadNavigationsAsync(link);
            return ApiResponse<PaymentLinkResponseDto>.Ok(MapToResponse(link), "Payment link updated");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var link = await _unitOfWork.PaymentLinks.GetByIdAsync(id);
            if (link == null)
                return ApiResponse<bool>.Fail("Payment link not found");

            var paid = await _unitOfWork.PaymentLinkTransactions.FindAsync(
                t => t.PaymentLinkId == id && t.Status == PaymentStatus.Paid);
            if (paid.Any())
                return ApiResponse<bool>.Fail("Cannot delete a link with successful transactions");

            await _unitOfWork.PaymentLinks.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Payment link deleted");
        }

        // ════════════════════════════════════════════════════════════════════
        // STATUS MANAGEMENT
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<bool>> ActivateAsync(Guid id)
        {
            var link = await _unitOfWork.PaymentLinks.GetByIdAsync(id);
            if (link == null)
                return ApiResponse<bool>.Fail("Payment link not found");

            if (link.Status == PaymentLinkStatus.Paid)
                return ApiResponse<bool>.Fail("Cannot activate a paid link");

            if (link.ExpiresAt.HasValue && link.ExpiresAt.Value <= DateTime.UtcNow)
                return ApiResponse<bool>.Fail("Cannot activate an expired link — update the expiry date first");

            link.Status = PaymentLinkStatus.Active;
            link.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.PaymentLinks.UpdateAsync(link);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Payment link activated");
        }

        public async Task<ApiResponse<bool>> DeactivateAsync(Guid id)
        {
            var link = await _unitOfWork.PaymentLinks.GetByIdAsync(id);
            if (link == null)
                return ApiResponse<bool>.Fail("Payment link not found");

            link.Status = PaymentLinkStatus.Inactive;
            link.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.PaymentLinks.UpdateAsync(link);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Payment link deactivated");
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PaymentLinkPublicDto>> GetPublicInfoAsync(string code)
        {
            var links = await _unitOfWork.PaymentLinks.FindAsync(l => l.Code == code.ToUpper());
            var link = links.FirstOrDefault();

            if (link == null)
                return ApiResponse<PaymentLinkPublicDto>.Fail("Payment link not found");

            var (isValid, reason) = ValidateLink(link);

            var items = new List<PaymentLinkItemResponseDto>();
            if (link.LinkType == PaymentLinkType.ProductBased)
            {
                var linkItems = await _unitOfWork.PaymentLinkItems.FindAsync(i => i.PaymentLinkId == link.Id);
                items = linkItems.Select(i => new PaymentLinkItemResponseDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList();
            }

            var dto = new PaymentLinkPublicDto
            {
                Code = link.Code,
                Title = link.Title,
                Description = link.Description,
                Amount = link.Amount,
                Currency = link.Currency,
                LinkType = link.LinkType,
                IsValid = isValid,
                InvalidReason = reason,
                Items = items
            };

            return ApiResponse<PaymentLinkPublicDto>.Ok(dto);
        }

        // ════════════════════════════════════════════════════════════════════
        // PROCESS PAYMENT
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PaymentLinkTransactionResponseDto>> ProcessPaymentAsync(ProcessPaymentDto dto)
        {
            var links = await _unitOfWork.PaymentLinks.FindAsync(l => l.Code == dto.LinkCode.ToUpper());
            var link = links.FirstOrDefault();

            if (link == null)
                return ApiResponse<PaymentLinkTransactionResponseDto>.Fail("Payment link not found");

            var (isValid, reason) = ValidateLink(link);
            if (!isValid)
                return ApiResponse<PaymentLinkTransactionResponseDto>.Fail(reason);

            var transaction = new PaymentLinkTransaction
            {
                PaymentLinkId = link.Id,
                PayerName = dto.PayerName,
                PayerEmail = dto.PayerEmail,
                PayerPhone = dto.PayerPhone,
                Amount = link.Amount,
                Currency = link.Currency,
                GatewayName = dto.GatewayName,
                GatewayTransactionId = dto.GatewayTransactionId,
                GatewayResponse = dto.GatewayResponse,
                Status = PaymentStatus.Paid,
                PaidAt = DateTime.UtcNow,
                TenantId = link.TenantId
            };

            Guid? generatedOrderId = null;
            string? generatedOrderNumber = null;

            if (link.LinkType != PaymentLinkType.OrderBased)
            {
                var orderResult = await CreateOrderFromLinkAsync(link, dto);
                if (orderResult != null)
                {
                    transaction.GeneratedOrderId = orderResult.Id;
                    generatedOrderId = orderResult.Id;
                    generatedOrderNumber = orderResult.OrderNumber;
                }
            }
            else if (link.OrderId.HasValue)
            {
                await _orderService.UpdatePaymentStatusAsync(link.OrderId.Value, PaymentStatus.Paid);
                transaction.GeneratedOrderId = link.OrderId;
                generatedOrderId = link.OrderId;
            }

            await _unitOfWork.PaymentLinkTransactions.AddAsync(transaction);

            link.UsedCount++;
            link.UpdatedAt = DateTime.UtcNow;

            bool maxUsesReached = link.MaxUses.HasValue && link.UsedCount >= link.MaxUses.Value;
            if (maxUsesReached || link.LinkType == PaymentLinkType.OrderBased)
                link.Status = PaymentLinkStatus.Paid;

            await _unitOfWork.PaymentLinks.UpdateAsync(link);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync(
                entityName: "PaymentLinkTransaction",
                entityId: transaction.Id.ToString(),
                action: AuditAction.Create,
                userId: Guid.Empty,
                tenantId: link.TenantId,
                newValue: $"Payment received — Link: {link.Code}, Payer: {dto.PayerName}, Amount: {link.Amount} {link.Currency}, Gateway: {dto.GatewayName}");

            await SendPostPaymentNotificationsAsync(link, transaction, generatedOrderNumber);

            var response = MapTransaction(transaction);
            response.GeneratedOrderId = generatedOrderId;
            return ApiResponse<PaymentLinkTransactionResponseDto>.Ok(response, "Payment processed successfully");
        }

        // ════════════════════════════════════════════════════════════════════
        // TRANSACTIONS
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PagedResponse<PaymentLinkTransactionResponseDto>>> GetTransactionsAsync(
            Guid paymentLinkId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.PaymentLinkTransactions.GetPagedAsync(
                t => t.PaymentLinkId == paymentLinkId,
                pagination.Skip, pagination.PageSize);

            var result = PagedResponse<PaymentLinkTransactionResponseDto>.Create(
                items.Select(MapTransaction).ToList(), total, pagination);

            return ApiResponse<PagedResponse<PaymentLinkTransactionResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PagedResponse<PaymentLinkTransactionResponseDto>>> GetTransactionsByTenantAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.PaymentLinkTransactions.GetPagedAsync(
                t => t.TenantId == tenantId,
                pagination.Skip, pagination.PageSize);

            var result = PagedResponse<PaymentLinkTransactionResponseDto>.Create(
                items.Select(MapTransaction).ToList(), total, pagination);

            return ApiResponse<PagedResponse<PaymentLinkTransactionResponseDto>>.Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static (bool isValid, string reason) ValidateLink(PaymentLink link)
        {
            if (link.Status == PaymentLinkStatus.Inactive)
                return (false, "This payment link is inactive");

            if (link.Status == PaymentLinkStatus.Paid)
                return (false, "This payment link has already been fully paid");

            if (link.ExpiresAt.HasValue && link.ExpiresAt.Value <= DateTime.UtcNow)
                return (false, "This payment link has expired");

            if (link.MaxUses.HasValue && link.UsedCount >= link.MaxUses.Value)
                return (false, "This payment link has reached its maximum number of uses");

            return (true, string.Empty);
        }

        private async Task<string> GenerateUniqueCodeAsync()
        {
            string code;
            bool exists;
            do
            {
                code = "PL-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                var found = await _unitOfWork.PaymentLinks.FindAsync(l => l.Code == code);
                exists = found.Any();
            } while (exists);

            return code;
        }

        private async Task<Order?> CreateOrderFromLinkAsync(PaymentLink link, ProcessPaymentDto dto)
        {
            try
            {
                var items = new List<CreateOrderItemDto>();

                if (link.LinkType == PaymentLinkType.ProductBased)
                {
                    var linkItems = await _unitOfWork.PaymentLinkItems.FindAsync(i => i.PaymentLinkId == link.Id);
                    items = linkItems.Select(i => new CreateOrderItemDto
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList();
                }
                else if (link.LinkType == PaymentLinkType.FreeAmount)
                {
                    // مفيش منتجات — نضيف item وهمي بالمبلغ الكلي
                    items.Add(new CreateOrderItemDto
                    {
                        ProductId = Guid.Empty,
                        Quantity = 1,
                        UnitPrice = link.Amount
                    });
                }

                var orderDto = new CreateOrderDto
                {
                    CustomerName = dto.PayerName,
                    CustomerEmail = dto.PayerEmail,
                    CustomerPhone = dto.PayerPhone,
                    PaymentStatus = PaymentStatus.Paid,
                    Status = OrderStatus.Confirmed,
                    TenantId = link.TenantId ?? Guid.Empty,
                    Notes = $"Created from Payment Link: {link.Code}",
                    Items = items
                };

                var result = await _orderService.CreateAsync(orderDto);
                return result.Success ? await _unitOfWork.Orders.GetByIdAsync(result.Data!.Id) : null;
            }
            catch
            {
                return null;  // فشل إنشاء الأوردر مش بيوقف عملية الدفع
            }
        }

        private async Task NotifyLinkCreatedAsync(PaymentLink link)
        {
            try
            {
                var admins = await _unitOfWork.Users.FindAsync(
                    u => u.TenantId == link.TenantId && u.Role == UserRole.TenantAdmin);

                var baseUrl = _configuration["App:BaseUrl"] ?? "https://app.ecomplatform.com";
                var publicUrl = $"{baseUrl}/pay/{link.Code}";

                foreach (var admin in admins)
                    _ = _emailService.SendAsync(
                        admin.Email,
                        $"تم إنشاء رابط دفع: {link.Title}",
                        BuildLinkCreatedEmail(admin.FirstName ?? "Admin", link, publicUrl));
            }
            catch { /* إرسال الإيميل اختياري */ }
        }

        private async Task SendPostPaymentNotificationsAsync(
            PaymentLink link, PaymentLinkTransaction tx, string? orderNumber)
        {
            try
            {
                _ = _emailService.SendAsync(
                    tx.PayerEmail,
                    $"تأكيد الدفع — {link.Title}",
                    BuildPaymentConfirmationEmail(tx, link, orderNumber));

                var admins = await _unitOfWork.Users.FindAsync(
                    u => u.TenantId == link.TenantId && u.Role == UserRole.TenantAdmin);

                foreach (var admin in admins)
                {
                    await _notificationService.CreateAsync(new CreateNotificationDto
                    {
                        UserId = admin.Id,
                        Title = "💰 دفعة جديدة",
                        Message = $"{tx.PayerName} دفع {tx.Amount:N2} {tx.Currency} على رابط \"{link.Title}\"",
                        Type = NotificationType.Payment
                    });

                    _ = _emailService.SendAsync(
                        admin.Email,
                        $"دفعة جديدة: {link.Title}",
                        BuildAdminPaymentEmail(tx, link, orderNumber));
                }
            }
            catch { /* الإشعارات لا توقف العملية */ }
        }

        private static string BuildLinkCreatedEmail(string name, PaymentLink link, string publicUrl) => $@"
            <div dir='rtl' style='font-family:Arial;max-width:600px;margin:0 auto;'>
                <h2>مرحباً {name} 👋</h2>
                <p>تم إنشاء رابط دفع جديد بنجاح.</p>
                <table style='border-collapse:collapse;width:100%;'>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>العنوان</b></td><td style='padding:8px;'>{link.Title}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>المبلغ</b></td><td style='padding:8px;'>{link.Amount:N2} {link.Currency}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>الكود</b></td><td style='padding:8px;'>{link.Code}</td></tr>
                    {(link.ExpiresAt.HasValue ? $"<tr><td style='padding:8px;background:#f5f5f5;'><b>ينتهي في</b></td><td style='padding:8px;'>{link.ExpiresAt:dd/MM/yyyy HH:mm}</td></tr>" : "")}
                </table>
                <p style='margin-top:20px;'>
                    <a href='{publicUrl}' style='background:#007bff;color:#fff;padding:12px 24px;text-decoration:none;border-radius:6px;'>
                        عرض رابط الدفع
                    </a>
                </p>
            </div>";

        private static string BuildPaymentConfirmationEmail(
            PaymentLinkTransaction tx, PaymentLink link, string? orderNumber) => $@"
            <div dir='rtl' style='font-family:Arial;max-width:600px;margin:0 auto;'>
                <h2 style='color:#28a745;'>✓ تم استلام دفعتك بنجاح</h2>
                <p>شكراً {tx.PayerName}، تم تأكيد دفعتك.</p>
                <table style='border-collapse:collapse;width:100%;'>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>المبلغ</b></td><td style='padding:8px;'>{tx.Amount:N2} {tx.Currency}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>البند</b></td><td style='padding:8px;'>{link.Title}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>وقت الدفع</b></td><td style='padding:8px;'>{tx.PaidAt:dd/MM/yyyy HH:mm}</td></tr>
                    {(orderNumber != null ? $"<tr><td style='padding:8px;background:#f5f5f5;'><b>رقم الطلب</b></td><td style='padding:8px;'>{orderNumber}</td></tr>" : "")}
                </table>
            </div>";

        private static string BuildAdminPaymentEmail(
            PaymentLinkTransaction tx, PaymentLink link, string? orderNumber) => $@"
            <div dir='rtl' style='font-family:Arial;max-width:600px;margin:0 auto;'>
                <h2>💰 دفعة جديدة على رابط: {link.Title}</h2>
                <table style='border-collapse:collapse;width:100%;'>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>الدافع</b></td><td style='padding:8px;'>{tx.PayerName}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>البريد</b></td><td style='padding:8px;'>{tx.PayerEmail}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>المبلغ</b></td><td style='padding:8px;'>{tx.Amount:N2} {tx.Currency}</td></tr>
                    <tr><td style='padding:8px;background:#f5f5f5;'><b>البوابة</b></td><td style='padding:8px;'>{tx.GatewayName}</td></tr>
                    {(orderNumber != null ? $"<tr><td style='padding:8px;background:#f5f5f5;'><b>رقم الطلب</b></td><td style='padding:8px;'>{orderNumber}</td></tr>" : "")}
                </table>
            </div>";

        private async Task LoadNavigationsAsync(PaymentLink link)
        {
            if (link.OrderId.HasValue)
                link.Order = await _unitOfWork.Orders.GetByIdAsync(link.OrderId.Value);

            if (link.CreatedById.HasValue)
                link.CreatedBy = await _unitOfWork.Users.GetByIdAsync(link.CreatedById.Value);

            link.Items = (await _unitOfWork.PaymentLinkItems.FindAsync(i => i.PaymentLinkId == link.Id)).ToList();

            link.Transactions = (await _unitOfWork.PaymentLinkTransactions
                .FindAsync(t => t.PaymentLinkId == link.Id))
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }

        // ════════════════════════════════════════════════════════════════════
        // MAPPERS
        // ════════════════════════════════════════════════════════════════════

        private string BuildPublicUrl(string code)
        {
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://app.ecomplatform.com";
            return $"{baseUrl}/pay/{code}";
        }

        private PaymentLinkResponseDto MapToResponse(PaymentLink l) => new()
        {
            Id = l.Id,
            Code = l.Code,
            Title = l.Title,
            Description = l.Description,
            Amount = l.Amount,
            Currency = l.Currency,
            LinkType = l.LinkType,
            LinkTypeName = l.LinkType.ToString(),
            Status = l.Status,
            StatusName = l.Status.ToString(),
            OrderId = l.OrderId,
            OrderNumber = l.Order?.OrderNumber,
            ExpiresAt = l.ExpiresAt,
            MaxUses = l.MaxUses,
            UsedCount = l.UsedCount,
            IsExpired = l.ExpiresAt.HasValue && l.ExpiresAt.Value <= DateTime.UtcNow,
            SuccessRedirectUrl = l.SuccessRedirectUrl,
            FailureRedirectUrl = l.FailureRedirectUrl,
            Metadata = l.Metadata,
            CreatedByName = l.CreatedBy != null ? $"{l.CreatedBy.FirstName} {l.CreatedBy.LastName}".Trim() : string.Empty,
            TenantId = l.TenantId,
            CreatedAt = l.CreatedAt,
            PublicUrl = BuildPublicUrl(l.Code),
            Items = l.Items.Select(i => new PaymentLinkItemResponseDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            Transactions = l.Transactions.Select(MapTransaction).ToList()
        };

        private static PaymentLinkTransactionResponseDto MapTransaction(PaymentLinkTransaction t) => new()
        {
            Id = t.Id,
            PayerName = t.PayerName,
            PayerEmail = t.PayerEmail,
            PayerPhone = t.PayerPhone,
            Amount = t.Amount,
            Currency = t.Currency,
            Status = t.Status,
            StatusName = t.Status.ToString(),
            GatewayName = t.GatewayName,
            GatewayTransactionId = t.GatewayTransactionId,
            GeneratedOrderId = t.GeneratedOrderId,
            PaidAt = t.PaidAt,
            FailureReason = t.FailureReason,
            CreatedAt = t.CreatedAt
        };
    }
}