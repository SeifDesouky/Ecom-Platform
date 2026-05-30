using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Inventory;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IInventoryService
    {
        // ── Warehouses ────────────────────────────────────────────────────────
        Task<ApiResponse<WarehouseResponseDto>> CreateWarehouseAsync(CreateWarehouseDto dto);
        Task<ApiResponse<WarehouseResponseDto>> GetWarehouseByIdAsync(Guid id);
        Task<ApiResponse<List<WarehouseResponseDto>>> GetWarehousesByTenantAsync(Guid tenantId);
        Task<ApiResponse<WarehouseResponseDto>> UpdateWarehouseAsync(Guid id, UpdateWarehouseDto dto);
        Task<ApiResponse<bool>> DeleteWarehouseAsync(Guid id);
        Task<ApiResponse<bool>> SetDefaultWarehouseAsync(Guid id);
        Task<ApiResponse<bool>> ToggleWarehouseStatusAsync(Guid id);

        // ── Stock Movements ───────────────────────────────────────────────────
        Task<ApiResponse<StockMovementResponseDto>> AddMovementAsync(CreateStockMovementDto dto);
        Task<ApiResponse<StockMovementResponseDto>> AdjustStockAsync(StockAdjustmentDto dto);
        Task<ApiResponse<StockMovementResponseDto>> TransferStockAsync(StockTransferDto dto);

        Task<ApiResponse<PagedResponse<StockMovementResponseDto>>> GetMovementsByProductAsync(
            Guid productId, PaginationParams pagination);

        Task<ApiResponse<PagedResponse<StockMovementResponseDto>>> GetMovementsByWarehouseAsync(
            Guid warehouseId, PaginationParams pagination);

        Task<ApiResponse<PagedResponse<StockMovementResponseDto>>> GetMovementsByTenantAsync(
            Guid tenantId, PaginationParams pagination);

        // ── Stock Reports ─────────────────────────────────────────────────────
        Task<ApiResponse<ProductStockSummaryDto>> GetProductStockSummaryAsync(Guid productId);
        Task<ApiResponse<List<ProductStockSummaryDto>>> GetLowStockProductsAsync(Guid tenantId);
        Task<ApiResponse<List<ProductStockSummaryDto>>> GetOutOfStockProductsAsync(Guid tenantId);
    }
}
