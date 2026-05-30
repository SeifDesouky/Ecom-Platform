using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.PaymentLinks;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IPaymentLinkService
    {
        // ── CRUD ──────────────────────────────────────────────────────────
        Task<ApiResponse<PaymentLinkResponseDto>> CreateAsync(CreatePaymentLinkDto dto);
        Task<ApiResponse<PaymentLinkResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<PaymentLinkResponseDto>> GetByCodeAsync(string code);
        Task<ApiResponse<PagedResponse<PaymentLinkResponseDto>>> GetByTenantAsync(Guid tenantId, PaginationParams pagination);
        Task<ApiResponse<PaymentLinkResponseDto>> UpdateAsync(Guid id, UpdatePaymentLinkDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);

        // ── Status Management ──────────────────────────────────────────────
        Task<ApiResponse<bool>> ActivateAsync(Guid id);
        Task<ApiResponse<bool>> DeactivateAsync(Guid id);

        // ── Public — بدون Auth ────────────────────────────────────────────
        /// <summary>جلب بيانات الرابط للعرض العام (صفحة الدفع)</summary>
        Task<ApiResponse<PaymentLinkPublicDto>> GetPublicInfoAsync(string code);

        // ── Payment Processing ─────────────────────────────────────────────
        /// <summary>
        /// تسجيل دفعة على رابط — بعد تأكيد البوابة.
        /// بيعمل: تسجيل Transaction + إنشاء Order لو مطلوب + Webhook + Email + Notification.
        /// </summary>
        Task<ApiResponse<PaymentLinkTransactionResponseDto>> ProcessPaymentAsync(ProcessPaymentDto dto);

        // ── Transactions ───────────────────────────────────────────────────
        Task<ApiResponse<PagedResponse<PaymentLinkTransactionResponseDto>>> GetTransactionsAsync(
            Guid paymentLinkId, PaginationParams pagination);

        Task<ApiResponse<PagedResponse<PaymentLinkTransactionResponseDto>>> GetTransactionsByTenantAsync(
            Guid tenantId, PaginationParams pagination);
    }
}
