using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.CMS;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ICMSService
    {
        // Pages
        Task<ApiResponse<PageResponseDto>> CreatePageAsync(CreatePageDto dto);
        Task<ApiResponse<PageResponseDto>> GetPageBySlugAsync(string slug, Guid? tenantId);
        Task<ApiResponse<PageResponseDto>> GetPageByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<PageResponseDto>>> GetAllPagesAsync(Guid? tenantId, PaginationParams pagination);
        Task<ApiResponse<PageResponseDto>> UpdatePageAsync(Guid id, CreatePageDto dto);
        Task<ApiResponse<bool>> DeletePageAsync(Guid id);
        Task<ApiResponse<bool>> TogglePagePublishAsync(Guid id);

        // Articles
        Task<ApiResponse<ArticleResponseDto>> CreateArticleAsync(CreateArticleDto dto);
        Task<ApiResponse<ArticleResponseDto>> GetArticleBySlugAsync(string slug, Guid? tenantId);
        Task<ApiResponse<ArticleResponseDto>> GetArticleByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<ArticleResponseDto>>> GetAllArticlesAsync(Guid? tenantId, PaginationParams pagination);
        Task<ApiResponse<ArticleResponseDto>> UpdateArticleAsync(Guid id, CreateArticleDto dto);
        Task<ApiResponse<bool>> DeleteArticleAsync(Guid id);
        Task<ApiResponse<bool>> ToggleArticlePublishAsync(Guid id);
    }
}