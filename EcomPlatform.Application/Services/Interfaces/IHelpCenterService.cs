using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.HelpCenter;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IHelpCenterService
    {
        // ── Categories ────────────────────────────────────────────────────────
        Task<ApiResponse<HelpCategoryResponseDto>> CreateCategoryAsync(CreateHelpCategoryDto dto);
        Task<ApiResponse<HelpCategoryResponseDto>> GetCategoryByIdAsync(Guid id);
        Task<ApiResponse<HelpCategoryResponseDto>> GetCategoryBySlugAsync(string slug, Guid? tenantId);
        Task<ApiResponse<List<HelpCategoryResponseDto>>> GetCategoriesAsync(Guid? tenantId);
        Task<ApiResponse<PagedResponse<HelpCategoryResponseDto>>> GetCategoriesAdminAsync(PaginationParams pagination);
        Task<ApiResponse<HelpCategoryResponseDto>> UpdateCategoryAsync(Guid id, UpdateHelpCategoryDto dto);
        Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id);
        Task<ApiResponse<bool>> ToggleCategoryStatusAsync(Guid id);

        // ── Articles ──────────────────────────────────────────────────────────
        Task<ApiResponse<HelpArticleResponseDto>> CreateArticleAsync(CreateHelpArticleDto dto);
        Task<ApiResponse<HelpArticleResponseDto>> GetArticleByIdAsync(Guid id);
        Task<ApiResponse<HelpArticleResponseDto>> GetArticleBySlugAsync(string slug, Guid? tenantId);

        // ✅ Public — Published فقط
        Task<ApiResponse<PagedResponse<HelpArticleResponseDto>>> GetArticlesByCategoryAsync(Guid categoryId, PaginationParams pagination);

        // ✅ Admin — كل المقالات (Draft + Published)
        Task<ApiResponse<PagedResponse<HelpArticleResponseDto>>> GetArticlesByCategoryAdminAsync(Guid categoryId, PaginationParams pagination);

        Task<ApiResponse<List<HelpArticleResponseDto>>> GetFAQsAsync(Guid? tenantId);
        Task<ApiResponse<HelpArticleResponseDto>> UpdateArticleAsync(Guid id, UpdateHelpArticleDto dto);
        Task<ApiResponse<bool>> DeleteArticleAsync(Guid id);
        Task<ApiResponse<bool>> PublishArticleAsync(Guid id);
        Task<ApiResponse<bool>> UnpublishArticleAsync(Guid id);

        // ── Public Actions ────────────────────────────────────────────────────
        Task<ApiResponse<HelpSearchResultDto>> SearchAsync(string query, Guid? tenantId);
        Task IncrementViewCountAsync(Guid articleId);
        Task<ApiResponse<bool>> SubmitFeedbackAsync(Guid articleId, bool isHelpful);
    }
}