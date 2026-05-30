using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Reviews;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IReviewService
    {
        // ── العميل ───────────────────────────────────────────────────────────

        /// <summary>
        /// إرسال تقييم جديد.
        /// - يتحقق إن Rating بين 1 و5.
        /// - يتحقق إنه لم يُقيِّم المنتج من قبل (نفس CustomerId أو Email).
        /// - يضع الحالة Pending تلقائياً (أو Approved لو الـ setting مفعَّل).
        /// - يُفعِّل IsVerifiedPurchase لو العميل اشترى المنتج فعلاً.
        /// </summary>
        Task<ApiResponse<ReviewResponseDto>> SubmitAsync(CreateReviewDto dto);

        /// <summary>تصويت "مفيد" على تقييم</summary>
        Task<ApiResponse<bool>> MarkHelpfulAsync(Guid reviewId);

        // ── صاحب المتجر (TenantAdmin / TenantStaff) ─────────────────────────

        /// <summary>كل تقييمات التينانت مع فلترة بالحالة</summary>
        Task<ApiResponse<PagedResponse<ReviewResponseDto>>> GetAllByTenantAsync(
            Guid tenantId,
            ReviewStatus? status,
            PaginationParams pagination);

        /// <summary>كل تقييمات منتج معين</summary>
        Task<ApiResponse<PagedResponse<ReviewResponseDto>>> GetByProductAsync(
            Guid productId,
            ReviewStatus? status,
            PaginationParams pagination);

        Task<ApiResponse<ReviewResponseDto>> GetByIdAsync(Guid id);

        /// <summary>اعتماد أو رفض أو تحديد كسبام</summary>
        Task<ApiResponse<ReviewResponseDto>> UpdateStatusAsync(Guid id, UpdateReviewStatusDto dto);

        /// <summary>رد صاحب المتجر على تقييم</summary>
        Task<ApiResponse<ReviewResponseDto>> AddOwnerReplyAsync(Guid id, OwnerReplyDto dto);

        /// <summary>حذف التقييم نهائياً</summary>
        Task<ApiResponse<bool>> DeleteAsync(Guid id);

        // ── صفحة المنتج (Public) ─────────────────────────────────────────────

        /// <summary>ملخص التقييمات + أحدث المعتمدة — للعرض في صفحة المنتج</summary>
        Task<ApiResponse<ProductRatingSummaryDto>> GetProductSummaryAsync(Guid productId);
    }
}