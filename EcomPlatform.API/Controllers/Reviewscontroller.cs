using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Reviews;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IAuditLogService _auditLogService;

        public ReviewsController(
            IReviewService reviewService,
            IAuditLogService auditLogService)
        {
            _reviewService = reviewService;
            _auditLogService = auditLogService;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private Guid? GetUserId()
            => Guid.TryParse(User.FindFirstValue("userId"), out var id) ? id : null;

        private Guid? GetTenantId()
            => Guid.TryParse(User.FindFirstValue("tenantId"), out var id) ? id : null;

        private string GetIp()
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ════════════════════════════════════════════════════════════════
        // PUBLIC ENDPOINTS (بدون Auth — أي زائر أو عميل)
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/reviews/product/{productId}/summary
        /// <summary>ملخص التقييمات لمنتج — للعرض في صفحة المنتج</summary>
        [HttpGet("product/{productId}/summary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductSummary(Guid productId)
        {
            var result = await _reviewService.GetProductSummaryAsync(productId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // GET /api/v1/reviews/product/{productId}?status=Approved&pageNumber=1&pageSize=10
        /// <summary>كل تقييمات منتج — مع فلترة بالحالة</summary>
        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByProduct(
            Guid productId,
            [FromQuery] ReviewStatus? status,
            [FromQuery] PaginationParams pagination)
        {
            // زائر عادي يشوف Approved فقط — الأدمن يشوف كله
            var isStaff = User.Identity?.IsAuthenticated == true &&
                          (User.IsInRole("SuperAdmin") ||
                           User.IsInRole("TenantAdmin") ||
                           User.IsInRole("TenantStaff"));

            var effectiveStatus = isStaff ? status : ReviewStatus.Approved;

            var result = await _reviewService.GetByProductAsync(productId, effectiveStatus, pagination);
            return Ok(result);
        }

        // POST /api/v1/reviews
        /// <summary>إرسال تقييم جديد — يحتاج تسجيل دخول أو بيانات زائر</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Submit([FromBody] CreateReviewDto dto)
        {
            var result = await _reviewService.SubmitAsync(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // POST /api/v1/reviews/{id}/helpful
        /// <summary>تصويت "مفيد" على تقييم</summary>
        [HttpPost("{id}/helpful")]
        [AllowAnonymous]
        public async Task<IActionResult> MarkHelpful(Guid id)
        {
            var result = await _reviewService.MarkHelpfulAsync(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // TENANT STAFF — إدارة التقييمات
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/reviews/tenant/{tenantId}?status=Pending&pageNumber=1&pageSize=20
        /// <summary>كل تقييمات التينانت — مع فلترة بالحالة</summary>
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(
            Guid tenantId,
            [FromQuery] ReviewStatus? status,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _reviewService.GetAllByTenantAsync(tenantId, status, pagination);
            return Ok(result);
        }

        // GET /api/v1/reviews/{id}
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _reviewService.GetByIdAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // PATCH /api/v1/reviews/{id}/status
        /// <summary>Approve / Reject / Spam</summary>
        [HttpPatch("{id}/status")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReviewStatusDto dto)
        {
            var existing = await _reviewService.GetByIdAsync(id);
            if (!existing.Success) return NotFound(existing);

            var result = await _reviewService.UpdateStatusAsync(id, dto);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "ProductReview",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: GetTenantId(),
                oldValue: existing.Data!.Status.ToString(),
                newValue: dto.Status.ToString(),
                ipAddress: GetIp());

            return Ok(result);
        }

        // POST /api/v1/reviews/{id}/reply
        /// <summary>رد صاحب المتجر على تقييم</summary>
        [HttpPost("{id}/reply")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> AddOwnerReply(Guid id, [FromBody] OwnerReplyDto dto)
        {
            var result = await _reviewService.AddOwnerReplyAsync(id, dto);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "ProductReview",
                entityId: id.ToString(),
                action: AuditAction.Update,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: GetTenantId(),
                newValue: "Owner reply added",
                ipAddress: GetIp());

            return Ok(result);
        }

        // DELETE /api/v1/reviews/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _reviewService.GetByIdAsync(id);
            if (!existing.Success) return NotFound(existing);

            var result = await _reviewService.DeleteAsync(id);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "ProductReview",
                entityId: id.ToString(),
                action: AuditAction.Delete,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: GetTenantId(),
                oldValue: $"Review by {existing.Data!.ReviewerName} — Rating {existing.Data.Rating}",
                ipAddress: GetIp());

            return Ok(result);
        }
    }
}