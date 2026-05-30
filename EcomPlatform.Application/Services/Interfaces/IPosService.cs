using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Dashboard;
using EcomPlatform.Application.DTOs.Pos;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IPosService
    {
        // ── Sessions ──────────────────────────────────────────────────────────
        /// <summary>افتح session جديدة — يتحقق إنه مفيش session مفتوحة للكاشير ده</summary>
        Task<ApiResponse<PosSessionResponseDto>> OpenSessionAsync(OpenPosSessionDto dto, Guid cashierId);

        /// <summary>أغلق الـ session واحسب الفروقات النقدية</summary>
        Task<ApiResponse<PosSessionSummaryDto>> CloseSessionAsync(Guid sessionId, ClosePosSessionDto dto, Guid cashierId);

        /// <summary>الـ session المفتوحة الحالية للكاشير</summary>
        Task<ApiResponse<PosSessionResponseDto>> GetActiveSessionAsync(Guid tenantId, Guid cashierId);

        /// <summary>كل السيشنز للتينانت — مع pagination</summary>
        Task<ApiResponse<PagedResponse<PosSessionResponseDto>>> GetSessionsAsync(Guid tenantId, PaginationParams pagination);

        Task<ApiResponse<PosSessionResponseDto>> GetSessionByIdAsync(Guid sessionId);

        // ── Orders / Sales ────────────────────────────────────────────────────
        /// <summary>أنشئ عملية بيع كاملة وخصم الستوك فوراً</summary>
        Task<ApiResponse<PosOrderResponseDto>> CreateOrderAsync(CreatePosOrderDto dto, Guid cashierId);

        /// <summary>ألغِ الفاتورة وأعد الستوك</summary>
        Task<ApiResponse<bool>> VoidOrderAsync(Guid orderId, VoidPosOrderDto dto, Guid cashierId);

        /// <summary>بيانات الفاتورة كاملة للطباعة الحرارية</summary>
        Task<ApiResponse<PosOrderResponseDto>> GetOrderReceiptAsync(Guid orderId);

        /// <summary>كل طلبات السيشن</summary>
        Task<ApiResponse<List<PosOrderResponseDto>>> GetSessionOrdersAsync(Guid sessionId);

        // ── Products ──────────────────────────────────────────────────────────
        /// <summary>بحث سريع بالاسم أو SKU أو الباركود</summary>
        Task<ApiResponse<List<PosProductDto>>> SearchProductsAsync(Guid tenantId, string query);

        /// <summary>جيب المنتج بالباركود مباشرة (Barcode Scan)</summary>
        Task<ApiResponse<PosProductDto>> GetProductByBarcodeAsync(Guid tenantId, string barcode);
    }
}
