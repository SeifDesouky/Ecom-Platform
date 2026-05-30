using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Inventory;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/v1/inventory")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        // ════════════════════════════════════════════════════════════════════
        // WAREHOUSES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>جلب كل مستودعات الـ tenant</summary>
        [HttpGet("warehouses/tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetWarehouses(Guid tenantId)
        {
            var result = await _inventoryService.GetWarehousesByTenantAsync(tenantId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>جلب مستودع بالـ ID</summary>
        [HttpGet("warehouses/{id:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetWarehouse(Guid id)
        {
            var result = await _inventoryService.GetWarehouseByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>إنشاء مستودع جديد</summary>
        [HttpPost("warehouses")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateWarehouse([FromBody] CreateWarehouseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var result = await _inventoryService.CreateWarehouseAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تعديل مستودع</summary>
        [HttpPut("warehouses/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] UpdateWarehouseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var result = await _inventoryService.UpdateWarehouseAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>حذف مستودع</summary>
        [HttpDelete("warehouses/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteWarehouse(Guid id)
        {
            var result = await _inventoryService.DeleteWarehouseAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تعيين مستودع كـ default</summary>
        [HttpPatch("warehouses/{id:guid}/set-default")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> SetDefault(Guid id)
        {
            var result = await _inventoryService.SetDefaultWarehouseAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تفعيل / تعطيل مستودع</summary>
        [HttpPatch("warehouses/{id:guid}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleWarehouse(Guid id)
        {
            var result = await _inventoryService.ToggleWarehouseStatusAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // STOCK MOVEMENTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>إضافة حركة مخزون (شراء / بيع / إرجاع / خسارة)</summary>
        [HttpPost("movements")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> AddMovement([FromBody] CreateStockMovementDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var result = await _inventoryService.AddMovementAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تسوية مخزون يدوية (جرد)</summary>
        [HttpPost("movements/adjust")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> AdjustStock([FromBody] StockAdjustmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var result = await _inventoryService.AdjustStockAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>نقل مخزون بين مستودعين</summary>
        [HttpPost("movements/transfer")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> TransferStock([FromBody] StockTransferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var result = await _inventoryService.TransferStockAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>سجل حركات منتج معين</summary>
        [HttpGet("movements/product/{productId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetMovementsByProduct(
            Guid productId, [FromQuery] PaginationParams pagination)
        {
            var result = await _inventoryService.GetMovementsByProductAsync(productId, pagination);
            return Ok(result);
        }

        /// <summary>سجل حركات مستودع معين</summary>
        [HttpGet("movements/warehouse/{warehouseId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetMovementsByWarehouse(
            Guid warehouseId, [FromQuery] PaginationParams pagination)
        {
            var result = await _inventoryService.GetMovementsByWarehouseAsync(warehouseId, pagination);
            return Ok(result);
        }

        /// <summary>كل حركات الـ tenant (مع pagination)</summary>
        [HttpGet("movements/tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetMovementsByTenant(
            Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _inventoryService.GetMovementsByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // STOCK REPORTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>ملخص مخزون منتج مع توزيع المستودعات</summary>
        [HttpGet("stock/product/{productId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetProductStockSummary(Guid productId)
        {
            var result = await _inventoryService.GetProductStockSummaryAsync(productId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>منتجات قاربت على النفاد (low stock)</summary>
        [HttpGet("stock/low-stock/tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetLowStockProducts(Guid tenantId)
        {
            var result = await _inventoryService.GetLowStockProductsAsync(tenantId);
            return Ok(result);
        }

        /// <summary>منتجات نفذت من المخزون</summary>
        [HttpGet("stock/out-of-stock/tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetOutOfStockProducts(Guid tenantId)
        {
            var result = await _inventoryService.GetOutOfStockProductsAsync(tenantId);
            return Ok(result);
        }
    }
}
