using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Categories;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CategoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<CategoryResponseDto>> CreateAsync(CreateCategoryDto dto)
        {
            var existing = await _unitOfWork.Categories.FindAsync(c =>
                c.Slug == dto.Slug && c.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<CategoryResponseDto>.Fail("Slug already exists");

            var category = new Category
            {
                Name = dto.Name,
                Slug = dto.Slug,
                Description = dto.Description,
                Image = dto.Image,
                ParentId = dto.ParentId,
                TenantId = dto.TenantId,
                IsActive = true
            };

            await _unitOfWork.Categories.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CategoryResponseDto>.Ok(MapToDto(category), "Category created successfully");
        }

        public async Task<ApiResponse<CategoryResponseDto>> GetByIdAsync(Guid id)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<CategoryResponseDto>.Fail("Category not found");

            return ApiResponse<CategoryResponseDto>.Ok(MapToDto(category));
        }

        public async Task<ApiResponse<IEnumerable<CategoryResponseDto>>> GetAllByTenantAsync(Guid tenantId)
        {
            var categories = await _unitOfWork.Categories.FindAsync(c => c.TenantId == tenantId);
            var result = categories.Select(MapToDto);
            return ApiResponse<IEnumerable<CategoryResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<CategoryResponseDto>> UpdateAsync(Guid id, UpdateCategoryDto dto)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<CategoryResponseDto>.Fail("Category not found");

            category.Name = dto.Name;
            category.Description = dto.Description;
            category.Image = dto.Image;
            category.ParentId = dto.ParentId;

            await _unitOfWork.Categories.UpdateAsync(category);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CategoryResponseDto>.Ok(MapToDto(category), "Category updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<bool>.Fail("Category not found");

            await _unitOfWork.Categories.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Category deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<bool>.Fail("Category not found");

            category.IsActive = !category.IsActive;

            await _unitOfWork.Categories.UpdateAsync(category);
            await _unitOfWork.SaveChangesAsync();

            var message = category.IsActive ? "Category activated" : "Category deactivated";
            return ApiResponse<bool>.Ok(true, message);
        }

        private static CategoryResponseDto MapToDto(Category category) => new()
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            Image = category.Image,
            IsActive = category.IsActive,
            ParentId = category.ParentId,
            ParentName = category.Parent?.Name,
            TenantId = category.TenantId,
            CreatedAt = category.CreatedAt,
            ProductsCount = category.Products?.Count ?? 0
        };
    }
}