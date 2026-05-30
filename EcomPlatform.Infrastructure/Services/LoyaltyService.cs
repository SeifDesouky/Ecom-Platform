using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Loyalty;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class LoyaltyService : ILoyaltyService
    {
        private readonly IUnitOfWork _unitOfWork;

        // ── Setting keys (مضبوطة في جدول Settings) ───────────────────────────
        // loyalty_enabled          → "true" / "false"
        // loyalty_earn_per_amount  → كل كم ريال يكسب 1 نقطة  (default: "10")
        // loyalty_points_per_unit  → عدد النقاط لكل وحدة     (default: "1")
        // loyalty_redeem_per_point → قيمة النقطة بالريال      (default: "0.05")
        // loyalty_min_redeem       → أقل عدد نقاط للصرف       (default: "100")
        // loyalty_expiry_days      → أيام انتهاء الصلاحية      (default: "365")

        public LoyaltyService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════

        private async Task<string?> GetSettingAsync(Guid tenantId, string key)
        {
            var settings = await _unitOfWork.Settings.FindAsync(
                s => s.TenantId == tenantId && s.Key == key);
            return settings.FirstOrDefault()?.Value;
        }

        private async Task<int> GetCurrentBalanceAsync(Guid tenantId, Guid customerId)
        {
            var txns = await _unitOfWork.LoyaltyPoints.FindAsync(
                l => l.TenantId == tenantId && l.CustomerId == customerId);
            return txns.Sum(l => l.Points);
        }

        private async Task<(string Name, string Email)> GetCustomerInfoAsync(Guid customerId)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
            if (customer == null) return (string.Empty, string.Empty);
            return ($"{customer.FirstName} {customer.LastName}".Trim(), customer.Email);
        }

        private static LoyaltyTransactionDto MapToDto(LoyaltyPoint lp, string customerName) => new()
        {
            Id = lp.Id,
            CustomerId = lp.CustomerId,
            CustomerName = customerName,
            Type = lp.Type,
            TypeLabel = lp.Type.ToString(),
            Points = lp.Points,
            BalanceAfter = lp.BalanceAfter,
            Reference = lp.Reference,
            Notes = lp.Notes,
            ExpiresAt = lp.ExpiresAt,
            IsExpired = lp.ExpiresAt.HasValue && lp.ExpiresAt < DateTime.UtcNow,
            CreatedAt = lp.CreatedAt
        };

        // ════════════════════════════════════════════════════════════════
        // BALANCE
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<LoyaltyBalanceDto>> GetBalanceAsync(
            Guid tenantId, Guid customerId)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
            if (customer == null || customer.TenantId != tenantId)
                return ApiResponse<LoyaltyBalanceDto>.Fail("Customer not found.");

            var balance = await GetCurrentBalanceAsync(tenantId, customerId);

            // قيمة النقطة بالعملة
            var redeemPerPoint = decimal.TryParse(
                await GetSettingAsync(tenantId, "loyalty_redeem_per_point"),
                out var rpp) ? rpp : 0.05m;

            // نقاط ستنتهي خلال 30 يوم
            var cutoff = DateTime.UtcNow.AddDays(30);
            var expiring = await _unitOfWork.LoyaltyPoints.FindAsync(l =>
                l.TenantId == tenantId &&
                l.CustomerId == customerId &&
                l.Points > 0 &&
                l.ExpiresAt != null &&
                l.ExpiresAt <= cutoff &&
                l.ExpiresAt > DateTime.UtcNow);

            var expiringPoints = expiring.Sum(l => l.Points);
            var nearestExpiry = expiring.OrderBy(l => l.ExpiresAt).FirstOrDefault()?.ExpiresAt;

            return ApiResponse<LoyaltyBalanceDto>.Ok(new LoyaltyBalanceDto
            {
                CustomerId = customerId,
                CustomerName = $"{customer.FirstName} {customer.LastName}".Trim(),
                Balance = balance,
                MonetaryValue = balance * redeemPerPoint,
                Currency = "SAR",
                ExpiringPoints = expiringPoints,
                NearestExpiry = nearestExpiry
            });
        }

        // ════════════════════════════════════════════════════════════════
        // EARN FROM ORDER
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<LoyaltyTransactionDto>> EarnFromOrderAsync(
            Guid tenantId, Guid customerId, Guid orderId, decimal orderTotal)
        {
            // تحقق إن الـ loyalty مفعَّل
            var enabled = await GetSettingAsync(tenantId, "loyalty_enabled");
            if (enabled != "true")
                return ApiResponse<LoyaltyTransactionDto>.Fail("Loyalty program is disabled.");

            // تأكد إنه لم يكتسب نقاط لهذا الأوردر من قبل
            var alreadyEarned = await _unitOfWork.LoyaltyPoints.FindAsync(l =>
                l.TenantId == tenantId &&
                l.CustomerId == customerId &&
                l.Reference == orderId.ToString() &&
                l.Type == LoyaltyTransactionType.Earned);

            if (alreadyEarned.Any())
                return ApiResponse<LoyaltyTransactionDto>.Fail(
                    "Points already earned for this order.");

            // احسب النقاط
            var earnPerAmount = decimal.TryParse(
                await GetSettingAsync(tenantId, "loyalty_earn_per_amount"),
                out var epa) ? epa : 10m;

            var pointsPerUnit = int.TryParse(
                await GetSettingAsync(tenantId, "loyalty_points_per_unit"),
                out var ppu) ? ppu : 1;

            int pointsEarned = (int)Math.Floor(orderTotal / earnPerAmount) * pointsPerUnit;
            if (pointsEarned <= 0)
                return ApiResponse<LoyaltyTransactionDto>.Fail(
                    "Order total is too low to earn points.");

            // تاريخ انتهاء الصلاحية
            var expiryDays = int.TryParse(
                await GetSettingAsync(tenantId, "loyalty_expiry_days"),
                out var ed) ? ed : 365;

            int currentBalance = await GetCurrentBalanceAsync(tenantId, customerId);

            var entry = new LoyaltyPoint
            {
                TenantId = tenantId,
                CustomerId = customerId,
                Type = LoyaltyTransactionType.Earned,
                Points = pointsEarned,
                BalanceAfter = currentBalance + pointsEarned,
                Reference = orderId.ToString(),
                Notes = $"Earned from order #{orderId}",
                ExpiresAt = expiryDays > 0
                                   ? DateTime.UtcNow.AddDays(expiryDays)
                                   : null
            };

            await _unitOfWork.LoyaltyPoints.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            var (name, _) = await GetCustomerInfoAsync(customerId);
            return ApiResponse<LoyaltyTransactionDto>.Ok(
                MapToDto(entry, name),
                $"{pointsEarned} points earned successfully.");
        }

        // ════════════════════════════════════════════════════════════════
        // REDEEM
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<RedeemResultDto>> RedeemAsync(RedeemLoyaltyDto dto)
        {
            // تحقق إن الـ loyalty مفعَّل
            var enabled = await GetSettingAsync(dto.TenantId, "loyalty_enabled");
            if (enabled != "true")
                return ApiResponse<RedeemResultDto>.Fail("Loyalty program is disabled.");

            if (dto.Points <= 0)
                return ApiResponse<RedeemResultDto>.Fail("Points to redeem must be positive.");

            // أقل عدد للصرف
            var minRedeem = int.TryParse(
                await GetSettingAsync(dto.TenantId, "loyalty_min_redeem"),
                out var mr) ? mr : 100;

            if (dto.Points < minRedeem)
                return ApiResponse<RedeemResultDto>.Fail(
                    $"Minimum redemption is {minRedeem} points.");

            // تحقق من الرصيد
            int balance = await GetCurrentBalanceAsync(dto.TenantId, dto.CustomerId);
            if (balance < dto.Points)
                return ApiResponse<RedeemResultDto>.Fail(
                    $"Insufficient points. Balance: {balance}, requested: {dto.Points}.");

            // قيمة الخصم
            var redeemPerPoint = decimal.TryParse(
                await GetSettingAsync(dto.TenantId, "loyalty_redeem_per_point"),
                out var rpp) ? rpp : 0.05m;

            decimal discount = dto.Points * redeemPerPoint;

            var entry = new LoyaltyPoint
            {
                TenantId = dto.TenantId,
                CustomerId = dto.CustomerId,
                Type = LoyaltyTransactionType.Redeemed,
                Points = -dto.Points,          // سالب
                BalanceAfter = balance - dto.Points,
                Reference = dto.OrderReference,
                Notes = $"Redeemed {dto.Points} pts = {discount:F2} SAR on order {dto.OrderReference}"
            };

            await _unitOfWork.LoyaltyPoints.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<RedeemResultDto>.Ok(new RedeemResultDto
            {
                PointsRedeemed = dto.Points,
                DiscountAmount = discount,
                NewBalance = balance - dto.Points
            }, $"Redeemed {dto.Points} points = {discount:F2} SAR discount.");
        }

        // ════════════════════════════════════════════════════════════════
        // ADJUST (Manual)
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<LoyaltyTransactionDto>> AdjustAsync(AdjustLoyaltyDto dto)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
            if (customer == null || customer.TenantId != dto.TenantId)
                return ApiResponse<LoyaltyTransactionDto>.Fail("Customer not found.");

            if (dto.Points == 0)
                return ApiResponse<LoyaltyTransactionDto>.Fail("Points cannot be zero.");

            int balance = await GetCurrentBalanceAsync(dto.TenantId, dto.CustomerId);

            // لو خصم يدوي — تأكد الرصيد كافٍ
            if (dto.Points < 0 && balance + dto.Points < 0)
                return ApiResponse<LoyaltyTransactionDto>.Fail(
                    $"Insufficient balance. Current: {balance}.");

            var entry = new LoyaltyPoint
            {
                TenantId = dto.TenantId,
                CustomerId = dto.CustomerId,
                Type = dto.Type,
                Points = dto.Points,
                BalanceAfter = balance + dto.Points,
                Reference = dto.Reference,
                Notes = dto.Notes,
                ExpiresAt = dto.ExpiresAt
            };

            await _unitOfWork.LoyaltyPoints.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            var (name, _) = await GetCustomerInfoAsync(dto.CustomerId);
            return ApiResponse<LoyaltyTransactionDto>.Ok(
                MapToDto(entry, name),
                "Points adjusted successfully.");
        }

        // ════════════════════════════════════════════════════════════════
        // REFUND (after order cancel/return)
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<LoyaltyTransactionDto>> RefundPointsAsync(
            Guid tenantId, Guid customerId, Guid orderId)
        {
            // جيب معاملة الربح الأصلية لهذا الأوردر
            var original = (await _unitOfWork.LoyaltyPoints.FindAsync(l =>
                l.TenantId == tenantId &&
                l.CustomerId == customerId &&
                l.Reference == orderId.ToString() &&
                l.Type == LoyaltyTransactionType.Earned))
                .FirstOrDefault();

            if (original == null)
                return ApiResponse<LoyaltyTransactionDto>.Fail(
                    "No loyalty points found for this order.");

            // تحقق إنه لم يُعَد من قبل
            var alreadyRefunded = await _unitOfWork.LoyaltyPoints.FindAsync(l =>
                l.TenantId == tenantId &&
                l.CustomerId == customerId &&
                l.Reference == orderId.ToString() &&
                l.Type == LoyaltyTransactionType.Refunded);

            if (alreadyRefunded.Any())
                return ApiResponse<LoyaltyTransactionDto>.Fail(
                    "Points already refunded for this order.");

            int balance = await GetCurrentBalanceAsync(tenantId, customerId);

            // يُعيد نفس عدد النقاط المكتسبة (إشارة موجبة — إضافة للرصيد)
            var entry = new LoyaltyPoint
            {
                TenantId = tenantId,
                CustomerId = customerId,
                Type = LoyaltyTransactionType.Refunded,
                Points = original.Points,
                BalanceAfter = balance + original.Points,
                Reference = orderId.ToString(),
                Notes = $"Points refunded for cancelled/returned order #{orderId}"
            };

            await _unitOfWork.LoyaltyPoints.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            var (name, _) = await GetCustomerInfoAsync(customerId);
            return ApiResponse<LoyaltyTransactionDto>.Ok(
                MapToDto(entry, name),
                $"{original.Points} points refunded successfully.");
        }

        // ════════════════════════════════════════════════════════════════
        // HISTORY
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PagedResponse<LoyaltyTransactionDto>>> GetCustomerHistoryAsync(
            Guid tenantId, Guid customerId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.LoyaltyPoints.GetPagedAsync(
                l => l.TenantId == tenantId && l.CustomerId == customerId,
                pagination.Skip,
                pagination.PageSize);

            var (name, _) = await GetCustomerInfoAsync(customerId);

            var dtos = items
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => MapToDto(l, name))
                .ToList();

            return ApiResponse<PagedResponse<LoyaltyTransactionDto>>.Ok(
                PagedResponse<LoyaltyTransactionDto>.Create(dtos, total, pagination));
        }

        public async Task<ApiResponse<PagedResponse<LoyaltyTransactionDto>>> GetTenantHistoryAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.LoyaltyPoints.GetPagedAsync(
                l => l.TenantId == tenantId,
                pagination.Skip,
                pagination.PageSize);

            // جيب أسماء العملاء دفعة واحدة
            var customerIds = items.Select(l => l.CustomerId).Distinct().ToHashSet();
            var customers = (await _unitOfWork.Customers.FindAsync(
                                   c => customerIds.Contains(c.Id)))
                              .ToDictionary(
                                   c => c.Id,
                                   c => $"{c.FirstName} {c.LastName}".Trim());

            var dtos = items
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => MapToDto(l, customers.GetValueOrDefault(l.CustomerId, string.Empty)))
                .ToList();

            return ApiResponse<PagedResponse<LoyaltyTransactionDto>>.Ok(
                PagedResponse<LoyaltyTransactionDto>.Create(dtos, total, pagination));
        }

        // ════════════════════════════════════════════════════════════════
        // EXPIRE (Background Job)
        // ════════════════════════════════════════════════════════════════

        public async Task ExpirePointsAsync(Guid tenantId)
        {
            // جيب كل النقاط المكتسبة التي انتهت صلاحيتها
            var expired = (await _unitOfWork.LoyaltyPoints.FindAsync(l =>
                l.TenantId == tenantId &&
                l.Points > 0 &&
                l.ExpiresAt != null &&
                l.ExpiresAt <= DateTime.UtcNow &&
                l.Type == LoyaltyTransactionType.Earned))
                .ToList();

            if (!expired.Any()) return;

            // تجنب تكرار التقادم
            var alreadyExpired = (await _unitOfWork.LoyaltyPoints.FindAsync(l =>
                l.TenantId == tenantId &&
                l.Type == LoyaltyTransactionType.Expired))
                .Select(l => l.Reference)
                .ToHashSet();

            foreach (var earn in expired)
            {
                if (alreadyExpired.Contains(earn.Id.ToString())) continue;

                int balance = await GetCurrentBalanceAsync(tenantId, earn.CustomerId);
                int toExpire = Math.Min(earn.Points, Math.Max(0, balance));
                if (toExpire <= 0) continue;

                var entry = new LoyaltyPoint
                {
                    TenantId = tenantId,
                    CustomerId = earn.CustomerId,
                    Type = LoyaltyTransactionType.Expired,
                    Points = -toExpire,
                    BalanceAfter = balance - toExpire,
                    Reference = earn.Id.ToString(),
                    Notes = $"Points expired (earned on {earn.CreatedAt:yyyy-MM-dd})"
                };

                await _unitOfWork.LoyaltyPoints.AddAsync(entry);
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
