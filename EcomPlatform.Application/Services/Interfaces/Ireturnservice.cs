using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Returns;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IReturnService
    {
        // ── إنشاء ─────────────────────────────────────────────────────────
        Task<ApiResponse<ReturnRequestResponseDto>> CreateAsync(CreateReturnRequestDto dto);

        /// <summary>يُستدعى تلقائياً من OrderService عند Cancel</summary>
        Task<ApiResponse<ReturnRequestResponseDto>> CreateFromCancelAsync(Guid orderId, Guid tenantId);

        // ── قراءة ─────────────────────────────────────────────────────────
        Task<ApiResponse<ReturnRequestResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<ReturnRequestResponseDto>> GetByReturnNumberAsync(string returnNumber);
        Task<ApiResponse<PagedResponse<ReturnRequestResponseDto>>> GetByOrderAsync(Guid orderId);
        Task<ApiResponse<PagedResponse<ReturnRequestResponseDto>>> GetByTenantAsync(Guid tenantId, PaginationParams pagination);

        // ── مراجعة Admin ──────────────────────────────────────────────────
        Task<ApiResponse<ReturnRequestResponseDto>> ReviewAsync(Guid id, ReviewReturnRequestDto dto);

        // ── الاسترداد المالي ──────────────────────────────────────────────
        Task<ApiResponse<bool>> ProcessRefundAsync(ProcessRefundDto dto);

        // ── إلغاء من العميل ───────────────────────────────────────────────
        Task<ApiResponse<bool>> CancelByCustomerAsync(Guid id);
    }
}