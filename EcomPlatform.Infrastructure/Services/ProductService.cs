using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Products;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public ProductService(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<ApiResponse<ProductResponseDto>> CreateAsync(CreateProductDto dto)
        {
            var existing = await _unitOfWork.Products.FindAsync(p =>
                p.Slug == dto.Slug && p.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<ProductResponseDto>.Fail("Slug already exists");

            var product = new Product
            {
                Name = dto.Name,
                Slug = dto.Slug,
                Description = dto.Description,
                ShortDescription = dto.ShortDescription,
                Price = dto.Price,
                ComparePrice = dto.ComparePrice,
                CostPrice = dto.CostPrice,
                SKU = dto.SKU,
                Barcode = dto.Barcode,
                Stock = dto.Stock,
                LowStockAlert = dto.LowStockAlert,
                TrackInventory = dto.TrackInventory,
                IsFeatured = dto.IsFeatured,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                Weight = dto.Weight,
                TenantId = dto.TenantId,
                CategoryId = dto.CategoryId,
                IsActive = true,
                Status = ProductStatus.Active
            };

            if (dto.Images.Any())
            {
                product.Images = dto.Images.Select(i => new ProductImage
                {
                    Url = i.Url,
                    Alt = i.Alt,
                    SortOrder = i.SortOrder,
                    IsMain = i.IsMain
                }).ToList();
            }

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ProductResponseDto>.Ok(MapToDto(product), "Product created successfully");
        }

        public async Task<ApiResponse<ProductResponseDto>> GetByIdAsync(Guid id)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return ApiResponse<ProductResponseDto>.Fail("Product not found");

            return ApiResponse<ProductResponseDto>.Ok(MapToDto(product));
        }

        public async Task<ApiResponse<PagedResponse<ProductResponseDto>>> GetAllByTenantAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, totalCount) = await _unitOfWork.Products.GetPagedAsync(
                p => p.TenantId == tenantId,
                pagination.Skip,
                pagination.PageSize);

            var result = PagedResponse<ProductResponseDto>.Create(
                items.Select(MapToDto).ToList(), totalCount, pagination);

            return ApiResponse<PagedResponse<ProductResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PagedResponse<ProductResponseDto>>> GetByCategoryAsync(
            Guid categoryId, PaginationParams pagination)
        {
            var (items, totalCount) = await _unitOfWork.Products.GetPagedAsync(
                p => p.CategoryId == categoryId,
                pagination.Skip,
                pagination.PageSize);

            var result = PagedResponse<ProductResponseDto>.Create(
                items.Select(MapToDto).ToList(), totalCount, pagination);

            return ApiResponse<PagedResponse<ProductResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<ProductResponseDto>> UpdateAsync(Guid id, UpdateProductDto dto)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return ApiResponse<ProductResponseDto>.Fail("Product not found");

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.ShortDescription = dto.ShortDescription;
            product.Price = dto.Price;
            product.ComparePrice = dto.ComparePrice;
            product.CostPrice = dto.CostPrice;
            product.SKU = dto.SKU;
            product.Barcode = dto.Barcode;
            product.Stock = dto.Stock;
            product.LowStockAlert = dto.LowStockAlert;
            product.TrackInventory = dto.TrackInventory;
            product.IsFeatured = dto.IsFeatured;
            product.MetaTitle = dto.MetaTitle;
            product.MetaDescription = dto.MetaDescription;
            product.Weight = dto.Weight;
            product.CategoryId = dto.CategoryId;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ProductResponseDto>.Ok(MapToDto(product), "Product updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return ApiResponse<bool>.Fail("Product not found");

            await _unitOfWork.Products.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Product deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return ApiResponse<bool>.Fail("Product not found");

            product.IsActive = !product.IsActive;
            product.Status = product.IsActive ? ProductStatus.Active : ProductStatus.Inactive;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            var message = product.IsActive ? "Product activated" : "Product deactivated";
            return ApiResponse<bool>.Ok(true, message);
        }

        public async Task<ApiResponse<bool>> UpdateStockAsync(Guid id, int quantity)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return ApiResponse<bool>.Fail("Product not found");

            product.Stock = quantity;
            product.Status = quantity == 0 ? ProductStatus.OutOfStock : ProductStatus.Active;

            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            if (product.TrackInventory && product.LowStockAlert > 0 && quantity <= product.LowStockAlert)
            {
                var admins = await _unitOfWork.Users.FindAsync(u =>
                    u.TenantId == product.TenantId && u.Role == Core.Enums.UserRole.TenantAdmin);
                foreach (var admin in admins)
                {
                    _ = _emailService.SendLowStockAlertAsync(
                        admin.Email,
                        product.Name,
                        quantity,
                        product.LowStockAlert);
                }
            }

            return ApiResponse<bool>.Ok(true, "Stock updated successfully");
        }

        private static ProductResponseDto MapToDto(Product product) => new()
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
            Price = product.Price,
            ComparePrice = product.ComparePrice,
            CostPrice = product.CostPrice,
            SKU = product.SKU,
            Barcode = product.Barcode,
            Stock = product.Stock,
            LowStockAlert = product.LowStockAlert,
            TrackInventory = product.TrackInventory,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            Status = product.Status,
            MetaTitle = product.MetaTitle,
            MetaDescription = product.MetaDescription,
            Weight = product.Weight,
            TenantId = product.TenantId ?? Guid.Empty,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            CreatedAt = product.CreatedAt,
            Images = product.Images?.Select(i => new ProductImageResponseDto
            {
                Id = i.Id,
                Url = i.Url,
                Alt = i.Alt,
                SortOrder = i.SortOrder,
                IsMain = i.IsMain
            }).ToList() ?? new()
        };
    }
}