using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Inventory;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;
        private readonly IAccountingService _accountingService;

        public InventoryService(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IAuditLogService auditLogService,
            IAccountingService accountingService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _accountingService = accountingService;
        }

        // ════════════════════════════════════════════════════════════════════
        // WAREHOUSES
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<WarehouseResponseDto>> CreateWarehouseAsync(CreateWarehouseDto dto)
        {
            var existing = await _unitOfWork.Warehouses.FindAsync(
                w => w.Code == dto.Code && w.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<WarehouseResponseDto>.Fail("Warehouse code already exists");

            // لو IsDefault = true، نشيل Default من الباقيين
            if (dto.IsDefault)
                await ClearDefaultWarehouseAsync(dto.TenantId);

            var warehouse = new Warehouse
            {
                Name = dto.Name,
                Code = dto.Code.ToUpper(),
                Address = dto.Address,
                City = dto.City,
                Country = dto.Country,
                Phone = dto.Phone,
                ManagerName = dto.ManagerName,
                IsDefault = dto.IsDefault,
                TenantId = dto.TenantId
            };

            await _unitOfWork.Warehouses.AddAsync(warehouse);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync(
                entityName: "Warehouse",
                entityId: warehouse.Id.ToString(),
                action: AuditAction.Create,
                userId: Guid.Empty,
                tenantId: dto.TenantId,
                newValue: $"Warehouse '{warehouse.Name}' ({warehouse.Code}) created");

            return ApiResponse<WarehouseResponseDto>.Ok(MapWarehouse(warehouse), "Warehouse created successfully");
        }

        public async Task<ApiResponse<WarehouseResponseDto>> GetWarehouseByIdAsync(Guid id)
        {
            var wh = await _unitOfWork.Warehouses.GetByIdAsync(id);
            if (wh == null)
                return ApiResponse<WarehouseResponseDto>.Fail("Warehouse not found");
            return ApiResponse<WarehouseResponseDto>.Ok(MapWarehouse(wh));
        }

        public async Task<ApiResponse<List<WarehouseResponseDto>>> GetWarehousesByTenantAsync(Guid tenantId)
        {
            var warehouses = await _unitOfWork.Warehouses.FindAsync(w => w.TenantId == tenantId);
            var sorted = warehouses.OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToList();
            return ApiResponse<List<WarehouseResponseDto>>.Ok(sorted.Select(MapWarehouse).ToList());
        }

        public async Task<ApiResponse<WarehouseResponseDto>> UpdateWarehouseAsync(Guid id, UpdateWarehouseDto dto)
        {
            var wh = await _unitOfWork.Warehouses.GetByIdAsync(id);
            if (wh == null)
                return ApiResponse<WarehouseResponseDto>.Fail("Warehouse not found");

            wh.Name = dto.Name;
            wh.Address = dto.Address;
            wh.City = dto.City;
            wh.Country = dto.Country;
            wh.Phone = dto.Phone;
            wh.ManagerName = dto.ManagerName;
            wh.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Warehouses.UpdateAsync(wh);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<WarehouseResponseDto>.Ok(MapWarehouse(wh), "Warehouse updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteWarehouseAsync(Guid id)
        {
            var wh = await _unitOfWork.Warehouses.GetByIdAsync(id);
            if (wh == null)
                return ApiResponse<bool>.Fail("Warehouse not found");

            if (wh.IsDefault)
                return ApiResponse<bool>.Fail("Cannot delete the default warehouse");

            // Check if warehouse has movements
            var movements = await _unitOfWork.StockMovements.FindAsync(m => m.WarehouseId == id);
            if (movements.Any())
                return ApiResponse<bool>.Fail("Cannot delete warehouse with existing stock movements");

            await _unitOfWork.Warehouses.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Warehouse deleted successfully");
        }

        public async Task<ApiResponse<bool>> SetDefaultWarehouseAsync(Guid id)
        {
            var wh = await _unitOfWork.Warehouses.GetByIdAsync(id);
            if (wh == null)
                return ApiResponse<bool>.Fail("Warehouse not found");

            await ClearDefaultWarehouseAsync(wh.TenantId ?? Guid.Empty);

            wh.IsDefault = true;
            wh.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Warehouses.UpdateAsync(wh);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Default warehouse updated");
        }

        public async Task<ApiResponse<bool>> ToggleWarehouseStatusAsync(Guid id)
        {
            var wh = await _unitOfWork.Warehouses.GetByIdAsync(id);
            if (wh == null)
                return ApiResponse<bool>.Fail("Warehouse not found");

            if (wh.IsDefault && wh.IsActive)
                return ApiResponse<bool>.Fail("Cannot deactivate the default warehouse");

            wh.IsActive = !wh.IsActive;
            wh.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Warehouses.UpdateAsync(wh);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, wh.IsActive ? "Warehouse activated" : "Warehouse deactivated");
        }

        // ════════════════════════════════════════════════════════════════════
        // STOCK MOVEMENTS
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<StockMovementResponseDto>> AddMovementAsync(CreateStockMovementDto dto)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
            if (product == null)
                return ApiResponse<StockMovementResponseDto>.Fail("Product not found");

            if (!product.TrackInventory)
                return ApiResponse<StockMovementResponseDto>.Fail("This product does not track inventory");

            var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(dto.WarehouseId);
            if (warehouse == null)
                return ApiResponse<StockMovementResponseDto>.Fail("Warehouse not found");

            // حركات الخروج: Sale / Transfer / Damage
            bool isOutbound = dto.Type is StockMovementType.Sale
                                       or StockMovementType.Transfer
                                       or StockMovementType.Damage;

            int quantityChange = isOutbound ? -dto.Quantity : dto.Quantity;

            if (isOutbound && product.Stock < dto.Quantity)
                return ApiResponse<StockMovementResponseDto>.Fail(
                    $"Insufficient stock. Available: {product.Stock}, Requested: {dto.Quantity}");

            int qtyBefore = product.Stock;
            int qtyAfter = qtyBefore + quantityChange;

            var movement = new StockMovement
            {
                Type = dto.Type,
                Quantity = quantityChange,
                QuantityBefore = qtyBefore,
                QuantityAfter = qtyAfter,
                UnitCost = dto.UnitCost,
                Reference = dto.Reference,
                Notes = dto.Notes,
                ProductId = dto.ProductId,
                WarehouseId = dto.WarehouseId,
                FromWarehouseId = dto.FromWarehouseId,
                OrderId = dto.OrderId,
                CreatedById = dto.CreatedById,
                TenantId = dto.TenantId
            };

            // Update product stock
            product.Stock = qtyAfter;
            product.Status = qtyAfter == 0 ? ProductStatus.OutOfStock : ProductStatus.Active;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StockMovements.AddAsync(movement);
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            // ✅ قيد محاسبي تلقائي لحركة المخزون (Purchase / Sale / Damage فقط)
            if (dto.Type is StockMovementType.Purchase
                         or StockMovementType.Sale
                         or StockMovementType.Damage)
            {
                await _accountingService.CreateStockMovementEntryAsync(movement.Id, dto.TenantId);
            }

            // Low stock alert
            await CheckLowStockAlertAsync(product);

            await _auditLogService.LogAsync(
                entityName: "StockMovement",
                entityId: movement.Id.ToString(),
                action: AuditAction.Create,
                userId: dto.CreatedById ?? Guid.Empty,
                tenantId: dto.TenantId,
                oldValue: $"Stock: {qtyBefore}",
                newValue: $"Stock: {qtyAfter} ({dto.Type}: {dto.Quantity})");

            await LoadMovementNavigationsAsync(movement);
            return ApiResponse<StockMovementResponseDto>.Ok(MapMovement(movement), "Stock movement recorded");
        }

        public async Task<ApiResponse<StockMovementResponseDto>> AdjustStockAsync(StockAdjustmentDto dto)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
            if (product == null)
                return ApiResponse<StockMovementResponseDto>.Fail("Product not found");

            var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(dto.WarehouseId);
            if (warehouse == null)
                return ApiResponse<StockMovementResponseDto>.Fail("Warehouse not found");

            int qtyBefore = product.Stock;
            int diff = dto.NewQuantity - qtyBefore;

            var movement = new StockMovement
            {
                Type = StockMovementType.Adjustment,
                Quantity = diff,
                QuantityBefore = qtyBefore,
                QuantityAfter = dto.NewQuantity,
                Notes = dto.Notes,
                ProductId = dto.ProductId,
                WarehouseId = dto.WarehouseId,
                CreatedById = dto.CreatedById,
                TenantId = dto.TenantId,
                Reference = "MANUAL-ADJ"
            };

            product.Stock = dto.NewQuantity;
            product.Status = dto.NewQuantity == 0 ? ProductStatus.OutOfStock : ProductStatus.Active;
            product.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StockMovements.AddAsync(movement);
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            await CheckLowStockAlertAsync(product);

            await _auditLogService.LogAsync(
                entityName: "StockMovement",
                entityId: movement.Id.ToString(),
                action: AuditAction.Create,
                userId: dto.CreatedById ?? Guid.Empty,
                tenantId: dto.TenantId,
                oldValue: $"Stock: {qtyBefore}",
                newValue: $"Adjusted to: {dto.NewQuantity} (diff: {diff:+#;-#;0})");

            await LoadMovementNavigationsAsync(movement);
            return ApiResponse<StockMovementResponseDto>.Ok(MapMovement(movement), "Stock adjusted successfully");
        }

        public async Task<ApiResponse<StockMovementResponseDto>> TransferStockAsync(StockTransferDto dto)
        {
            if (dto.FromWarehouseId == dto.ToWarehouseId)
                return ApiResponse<StockMovementResponseDto>.Fail("Source and destination warehouses must be different");

            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
            if (product == null)
                return ApiResponse<StockMovementResponseDto>.Fail("Product not found");

            if (product.Stock < dto.Quantity)
                return ApiResponse<StockMovementResponseDto>.Fail(
                    $"Insufficient stock. Available: {product.Stock}, Requested: {dto.Quantity}");

            var fromWh = await _unitOfWork.Warehouses.GetByIdAsync(dto.FromWarehouseId);
            var toWh = await _unitOfWork.Warehouses.GetByIdAsync(dto.ToWarehouseId);

            if (fromWh == null || toWh == null)
                return ApiResponse<StockMovementResponseDto>.Fail("One or both warehouses not found");

            int qtyBefore = product.Stock;

            // حركة الخروج من المستودع الأصلي
            var outMovement = new StockMovement
            {
                Type = StockMovementType.Transfer,
                Quantity = -dto.Quantity,
                QuantityBefore = qtyBefore,
                QuantityAfter = qtyBefore,
                Notes = dto.Notes,
                ProductId = dto.ProductId,
                WarehouseId = dto.FromWarehouseId,
                FromWarehouseId = null,
                CreatedById = dto.CreatedById,
                TenantId = dto.TenantId,
                Reference = $"TRANSFER-OUT"
            };

            // حركة الدخول للمستودع الجديد
            var inMovement = new StockMovement
            {
                Type = StockMovementType.Transfer,
                Quantity = dto.Quantity,
                QuantityBefore = qtyBefore,
                QuantityAfter = qtyBefore,
                Notes = dto.Notes,
                ProductId = dto.ProductId,
                WarehouseId = dto.ToWarehouseId,
                FromWarehouseId = dto.FromWarehouseId,
                CreatedById = dto.CreatedById,
                TenantId = dto.TenantId,
                Reference = $"TRANSFER-IN"
            };

            await _unitOfWork.StockMovements.AddAsync(outMovement);
            await _unitOfWork.StockMovements.AddAsync(inMovement);
            await _unitOfWork.SaveChangesAsync();

            await LoadMovementNavigationsAsync(inMovement);
            return ApiResponse<StockMovementResponseDto>.Ok(MapMovement(inMovement),
                $"Transferred {dto.Quantity} units from {fromWh.Name} to {toWh.Name}");
        }

        public async Task<ApiResponse<PagedResponse<StockMovementResponseDto>>> GetMovementsByProductAsync(
            Guid productId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.StockMovements.GetPagedAsync(
                m => m.ProductId == productId,
                pagination.Skip, pagination.PageSize);

            var result = PagedResponse<StockMovementResponseDto>.Create(
                items.Select(MapMovement).ToList(), total, pagination);
            return ApiResponse<PagedResponse<StockMovementResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PagedResponse<StockMovementResponseDto>>> GetMovementsByWarehouseAsync(
            Guid warehouseId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.StockMovements.GetPagedAsync(
                m => m.WarehouseId == warehouseId,
                pagination.Skip, pagination.PageSize);

            var result = PagedResponse<StockMovementResponseDto>.Create(
                items.Select(MapMovement).ToList(), total, pagination);
            return ApiResponse<PagedResponse<StockMovementResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PagedResponse<StockMovementResponseDto>>> GetMovementsByTenantAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.StockMovements.GetPagedAsync(
                m => m.TenantId == tenantId,
                pagination.Skip, pagination.PageSize);

            var result = PagedResponse<StockMovementResponseDto>.Create(
                items.Select(MapMovement).ToList(), total, pagination);
            return ApiResponse<PagedResponse<StockMovementResponseDto>>.Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // STOCK REPORTS
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ProductStockSummaryDto>> GetProductStockSummaryAsync(Guid productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return ApiResponse<ProductStockSummaryDto>.Fail("Product not found");

            var warehouses = await _unitOfWork.Warehouses.FindAsync(
                w => w.TenantId == product.TenantId && w.IsActive);

            var movements = await _unitOfWork.StockMovements.FindAsync(
                m => m.ProductId == productId);

            var warehouseBreakdown = warehouses.Select(wh =>
            {
                var whMovements = movements.Where(m => m.WarehouseId == wh.Id);
                int stock = whMovements.Any() ? whMovements.OrderByDescending(m => m.CreatedAt).First().QuantityAfter : 0;
                return new WarehouseStockDto
                {
                    WarehouseId = wh.Id,
                    WarehouseName = wh.Name,
                    WarehouseCode = wh.Code,
                    Stock = Math.Max(0, stock)
                };
            }).ToList();

            var summary = new ProductStockSummaryDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSKU = product.SKU,
                TotalStock = product.Stock,
                LowStockAlert = product.LowStockAlert,
                IsLowStock = product.Stock <= product.LowStockAlert,
                WarehouseBreakdown = warehouseBreakdown
            };

            return ApiResponse<ProductStockSummaryDto>.Ok(summary);
        }

        public async Task<ApiResponse<List<ProductStockSummaryDto>>> GetLowStockProductsAsync(Guid tenantId)
        {
            var products = await _unitOfWork.Products.FindAsync(
                p => p.TenantId == tenantId &&
                     p.TrackInventory &&
                     p.Stock > 0 &&
                     p.Stock <= p.LowStockAlert);

            var result = products.Select(p => new ProductStockSummaryDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ProductSKU = p.SKU,
                TotalStock = p.Stock,
                LowStockAlert = p.LowStockAlert,
                IsLowStock = true
            }).OrderBy(p => p.TotalStock).ToList();

            return ApiResponse<List<ProductStockSummaryDto>>.Ok(result);
        }

        public async Task<ApiResponse<List<ProductStockSummaryDto>>> GetOutOfStockProductsAsync(Guid tenantId)
        {
            var products = await _unitOfWork.Products.FindAsync(
                p => p.TenantId == tenantId &&
                     p.TrackInventory &&
                     p.Stock == 0);

            var result = products.Select(p => new ProductStockSummaryDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ProductSKU = p.SKU,
                TotalStock = 0,
                LowStockAlert = p.LowStockAlert,
                IsLowStock = true
            }).ToList();

            return ApiResponse<List<ProductStockSummaryDto>>.Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        private async Task ClearDefaultWarehouseAsync(Guid tenantId)
        {
            var defaults = await _unitOfWork.Warehouses.FindAsync(
                w => w.TenantId == tenantId && w.IsDefault);
            foreach (var wh in defaults)
            {
                wh.IsDefault = false;
                await _unitOfWork.Warehouses.UpdateAsync(wh);
            }
        }

        private async Task CheckLowStockAlertAsync(Product product)
        {
            if (!product.TrackInventory || product.LowStockAlert <= 0) return;
            if (product.Stock > product.LowStockAlert) return;

            var admins = await _unitOfWork.Users.FindAsync(
                u => u.TenantId == product.TenantId && u.Role == UserRole.TenantAdmin);

            foreach (var admin in admins)
                _ = _emailService.SendLowStockAlertAsync(admin.Email, product.Name, product.Stock, product.LowStockAlert);
        }

        private async Task LoadMovementNavigationsAsync(StockMovement movement)
        {
            movement.Product = await _unitOfWork.Products.GetByIdAsync(movement.ProductId);
            movement.Warehouse = await _unitOfWork.Warehouses.GetByIdAsync(movement.WarehouseId);
            if (movement.FromWarehouseId.HasValue)
                movement.FromWarehouse = await _unitOfWork.Warehouses.GetByIdAsync(movement.FromWarehouseId.Value);
        }

        private static WarehouseResponseDto MapWarehouse(Warehouse wh) => new()
        {
            Id = wh.Id,
            Name = wh.Name,
            Code = wh.Code,
            Address = wh.Address,
            City = wh.City,
            Country = wh.Country,
            Phone = wh.Phone,
            ManagerName = wh.ManagerName,
            IsActive = wh.IsActive,
            IsDefault = wh.IsDefault,
            TenantId = wh.TenantId,
            CreatedAt = wh.CreatedAt
        };

        private static StockMovementResponseDto MapMovement(StockMovement m) => new()
        {
            Id = m.Id,
            Type = m.Type,
            TypeName = m.Type.ToString(),
            Quantity = m.Quantity,
            QuantityBefore = m.QuantityBefore,
            QuantityAfter = m.QuantityAfter,
            UnitCost = m.UnitCost,
            Reference = m.Reference,
            Notes = m.Notes,
            ProductId = m.ProductId,
            ProductName = m.Product?.Name ?? string.Empty,
            ProductSKU = m.Product?.SKU ?? string.Empty,
            WarehouseId = m.WarehouseId,
            WarehouseName = m.Warehouse?.Name ?? string.Empty,
            FromWarehouseId = m.FromWarehouseId,
            FromWarehouseName = m.FromWarehouse?.Name,
            OrderId = m.OrderId,
            OrderNumber = null,
            CreatedByName = m.CreatedBy != null
                                ? $"{m.CreatedBy.FirstName} {m.CreatedBy.LastName}".Trim()
                                : string.Empty,
            TenantId = m.TenantId,
            CreatedAt = m.CreatedAt
        };
    }
}