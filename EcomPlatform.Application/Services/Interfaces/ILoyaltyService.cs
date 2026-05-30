using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Loyalty;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ILoyaltyService
    {
        // ── Balance ───────────────────────────────────────────────────────────

        /// <summary>رصيد العميل الحالي + نقاط ستنتهي قريباً</summary>
        Task<ApiResponse<LoyaltyBalanceDto>> GetBalanceAsync(Guid tenantId, Guid customerId);

        // ── Earn (يُستدعى تلقائياً من OrderService عند الـ Delivery) ─────────

        /// <summary>
        /// احسب وأضف نقاط لأوردر مكتمل.
        /// القاعدة: كل X ريال = Y نقطة — مضبوطة في الـ Settings.
        /// </summary>
        Task<ApiResponse<LoyaltyTransactionDto>> EarnFromOrderAsync(
            Guid tenantId, Guid customerId, Guid orderId, decimal orderTotal);

        // ── Redeem ────────────────────────────────────────────────────────────

        /// <summary>
        /// صرف نقاط كخصم.
        /// يتحقق أن الرصيد كافٍ وأن النقاط لم تنتهِ.
        /// يرجع قيمة الخصم بالعملة.
        /// </summary>
        Task<ApiResponse<RedeemResultDto>> RedeemAsync(RedeemLoyaltyDto dto);

        // ── Admin ─────────────────────────────────────────────────────────────

        /// <summary>إضافة / خصم نقاط يدوياً (Bonus، تعديل، انتهاء)</summary>
        Task<ApiResponse<LoyaltyTransactionDto>> AdjustAsync(AdjustLoyaltyDto dto);

        /// <summary>إعادة نقاط بعد إلغاء أو إرجاع أوردر</summary>
        Task<ApiResponse<LoyaltyTransactionDto>> RefundPointsAsync(
            Guid tenantId, Guid customerId, Guid orderId);

        // ── History ───────────────────────────────────────────────────────────

        /// <summary>سجل معاملات عميل معين</summary>
        Task<ApiResponse<PagedResponse<LoyaltyTransactionDto>>> GetCustomerHistoryAsync(
            Guid tenantId, Guid customerId, PaginationParams pagination);

        /// <summary>كل معاملات التينانت (للداشبورد)</summary>
        Task<ApiResponse<PagedResponse<LoyaltyTransactionDto>>> GetTenantHistoryAsync(
            Guid tenantId, PaginationParams pagination);

        // ── Expiry (يُشغَّل من Background Job يومياً) ────────────────────────

        /// <summary>انتهِ صلاحية النقاط المنتهية وسجِّلها</summary>
        Task ExpirePointsAsync(Guid tenantId);
    }
}
