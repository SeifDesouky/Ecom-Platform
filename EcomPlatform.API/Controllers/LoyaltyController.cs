using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Loyalty;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/loyalty")]
    [Authorize]
    public class LoyaltyController : ControllerBase
    {
        private readonly ILoyaltyService _loyaltyService;
        private readonly IAuditLogService _auditLogService;

        public LoyaltyController(
            ILoyaltyService loyaltyService,
            IAuditLogService auditLogService)
        {
            _loyaltyService = loyaltyService;
            _auditLogService = auditLogService;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private Guid? GetUserId()
            => Guid.TryParse(User.FindFirstValue("userId"), out var id) ? id : null;

        private Guid? GetTenantId()
            => Guid.TryParse(User.FindFirstValue("tenantId"), out var id) ? id : null;

        private string GetIp()
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ════════════════════════════════════════════════════════════════
        // CUSTOMER — رصيده وسجله
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/loyalty/balance/{customerId}?tenantId=xxx
        /// <summary>رصيد النقاط الحالي للعميل</summary>
        [HttpGet("balance/{customerId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetBalance(
            Guid customerId,
            [FromQuery] Guid tenantId)
        {
            var result = await _loyaltyService.GetBalanceAsync(tenantId, customerId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // GET /api/v1/loyalty/my-balance?tenantId=xxx
        /// <summary>رصيد نقاط العميل المسجَّل (من الـ token)</summary>
        [HttpGet("my-balance")]
        public async Task<IActionResult> GetMyBalance([FromQuery] Guid tenantId)
        {
            // نحوّل userId لـ customerId عن طريق البحث
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            // نمرّر userId كـ customerId مباشرةً (لو Customer.Id = User.Id في نظامك)
            // لو مختلف — محتاج تعمل lookup في CustomerService
            var result = await _loyaltyService.GetBalanceAsync(tenantId, userId.Value);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // GET /api/v1/loyalty/history/{customerId}?tenantId=xxx
        /// <summary>سجل معاملات عميل معين</summary>
        [HttpGet("history/{customerId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetCustomerHistory(
            Guid customerId,
            [FromQuery] Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _loyaltyService.GetCustomerHistoryAsync(tenantId, customerId, pagination);
            return Ok(result);
        }

        // GET /api/v1/loyalty/my-history?tenantId=xxx
        /// <summary>سجل نقاط العميل المسجَّل</summary>
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory(
            [FromQuery] Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _loyaltyService.GetCustomerHistoryAsync(tenantId, userId.Value, pagination);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // REDEEM — صرف نقاط
        // ════════════════════════════════════════════════════════════════

        // POST /api/v1/loyalty/redeem
        /// <summary>صرف نقاط كخصم على أوردر</summary>
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem([FromBody] RedeemLoyaltyDto dto)
        {
            var result = await _loyaltyService.RedeemAsync(dto);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "LoyaltyPoints",
                entityId: dto.CustomerId.ToString(),
                action: AuditAction.Update,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: dto.TenantId,
                newValue: $"Redeemed {dto.Points} pts on order {dto.OrderReference}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // ADMIN — تعديل يدوي
        // ════════════════════════════════════════════════════════════════

        // POST /api/v1/loyalty/adjust
        /// <summary>إضافة / خصم نقاط يدوياً (Bonus أو تعديل)</summary>
        [HttpPost("adjust")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Adjust([FromBody] AdjustLoyaltyDto dto)
        {
            var result = await _loyaltyService.AdjustAsync(dto);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "LoyaltyPoints",
                entityId: dto.CustomerId.ToString(),
                action: AuditAction.Update,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: dto.TenantId,
                newValue: $"{dto.Type}: {dto.Points} pts — {dto.Notes}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // POST /api/v1/loyalty/refund/{customerId}/{orderId}?tenantId=xxx
        /// <summary>إعادة نقاط بعد إلغاء أو إرجاع أوردر</summary>
        [HttpPost("refund/{customerId}/{orderId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> RefundPoints(
            Guid customerId,
            Guid orderId,
            [FromQuery] Guid tenantId)
        {
            var result = await _loyaltyService.RefundPointsAsync(tenantId, customerId, orderId);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "LoyaltyPoints",
                entityId: customerId.ToString(),
                action: AuditAction.Update,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: tenantId,
                newValue: $"Points refunded for order {orderId}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // TENANT DASHBOARD
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/loyalty/tenant/{tenantId}/history
        /// <summary>كل معاملات النقاط للتينانت</summary>
        [HttpGet("tenant/{tenantId}/history")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetTenantHistory(
            Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _loyaltyService.GetTenantHistoryAsync(tenantId, pagination);
            return Ok(result);
        }

        // POST /api/v1/loyalty/tenant/{tenantId}/expire
        /// <summary>تشغيل انتهاء صلاحية النقاط يدوياً (أو من Background Job)</summary>
        [HttpPost("tenant/{tenantId}/expire")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ExpirePoints(Guid tenantId)
        {
            await _loyaltyService.ExpirePointsAsync(tenantId);
            return Ok(ApiResponse<bool>.Ok(true, "Expiry process completed."));
        }
    }
}
