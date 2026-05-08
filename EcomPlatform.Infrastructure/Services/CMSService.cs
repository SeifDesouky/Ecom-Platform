using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.CMS;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class CMSService : ICMSService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CMSService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<PageResponseDto>> CreatePageAsync(CreatePageDto dto)
        {
            var existing = await _unitOfWork.Pages.FindAsync(p =>
                p.Slug == dto.Slug && p.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<PageResponseDto>.Fail("Slug already exists");

            var page = new Page
            {
                Title = dto.Title,
                Slug = dto.Slug,
                Content = dto.Content,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                IsPublished = dto.IsPublished,
                Type = dto.Type,
                SortOrder = dto.SortOrder,
                TenantId = dto.TenantId
            };

            await _unitOfWork.Pages.AddAsync(page);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<PageResponseDto>.Ok(MapPageToDto(page), "Page created successfully");
        }

        public async Task<ApiResponse<PageResponseDto>> GetPageBySlugAsync(string slug, Guid? tenantId)
        {
            var pages = await _unitOfWork.Pages.FindAsync(p =>
                p.Slug == slug && p.TenantId == tenantId && p.IsPublished);
            var page = pages.FirstOrDefault();

            if (page == null)
                return ApiResponse<PageResponseDto>.Fail("Page not found");

            return ApiResponse<PageResponseDto>.Ok(MapPageToDto(page));
        }

        public async Task<ApiResponse<PageResponseDto>> GetPageByIdAsync(Guid id)
        {
            var page = await _unitOfWork.Pages.GetByIdAsync(id);
            if (page == null)
                return ApiResponse<PageResponseDto>.Fail("Page not found");

            return ApiResponse<PageResponseDto>.Ok(MapPageToDto(page));
        }

        public async Task<ApiResponse<PagedResponse<PageResponseDto>>> GetAllPagesAsync(Guid? tenantId, PaginationParams pagination)
        {
            var all = await _unitOfWork.Pages.FindAsync(p => p.TenantId == tenantId);
            var totalCount = all.Count();
            var items = all
                .OrderBy(p => p.SortOrder)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .Select(MapPageToDto)
                .ToList();
            var result = PagedResponse<PageResponseDto>.Create(items, totalCount, pagination);
            return ApiResponse<PagedResponse<PageResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PageResponseDto>> UpdatePageAsync(Guid id, CreatePageDto dto)
        {
            var page = await _unitOfWork.Pages.GetByIdAsync(id);
            if (page == null)
                return ApiResponse<PageResponseDto>.Fail("Page not found");

            page.Title = dto.Title;
            page.Content = dto.Content;
            page.MetaTitle = dto.MetaTitle;
            page.MetaDescription = dto.MetaDescription;
            page.IsPublished = dto.IsPublished;
            page.Type = dto.Type;
            page.SortOrder = dto.SortOrder;

            await _unitOfWork.Pages.UpdateAsync(page);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<PageResponseDto>.Ok(MapPageToDto(page), "Page updated successfully");
        }

        public async Task<ApiResponse<bool>> DeletePageAsync(Guid id)
        {
            var page = await _unitOfWork.Pages.GetByIdAsync(id);
            if (page == null)
                return ApiResponse<bool>.Fail("Page not found");

            await _unitOfWork.Pages.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Page deleted successfully");
        }

        public async Task<ApiResponse<bool>> TogglePagePublishAsync(Guid id)
        {
            var page = await _unitOfWork.Pages.GetByIdAsync(id);
            if (page == null)
                return ApiResponse<bool>.Fail("Page not found");

            page.IsPublished = !page.IsPublished;
            await _unitOfWork.Pages.UpdateAsync(page);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, page.IsPublished ? "Page published" : "Page unpublished");
        }

        public async Task<ApiResponse<ArticleResponseDto>> CreateArticleAsync(CreateArticleDto dto)
        {
            var existing = await _unitOfWork.Articles.FindAsync(a =>
                a.Slug == dto.Slug && a.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<ArticleResponseDto>.Fail("Slug already exists");

            var article = new Article
            {
                Title = dto.Title,
                Slug = dto.Slug,
                Content = dto.Content,
                Excerpt = dto.Excerpt,
                CoverImage = dto.CoverImage,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                IsPublished = dto.IsPublished,
                PublishedAt = dto.IsPublished ? DateTime.UtcNow : null,
                Tags = dto.Tags,
                TenantId = dto.TenantId,
                AuthorId = dto.AuthorId
            };

            await _unitOfWork.Articles.AddAsync(article);
            await _unitOfWork.SaveChangesAsync();

            var author = await _unitOfWork.Users.GetByIdAsync(dto.AuthorId);
            article.Author = author;

            return ApiResponse<ArticleResponseDto>.Ok(MapArticleToDto(article), "Article created successfully");
        }

        public async Task<ApiResponse<ArticleResponseDto>> GetArticleBySlugAsync(string slug, Guid? tenantId)
        {
            var articles = await _unitOfWork.Articles.FindAsync(a =>
                a.Slug == slug && a.TenantId == tenantId && a.IsPublished);
            var article = articles.FirstOrDefault();

            if (article == null)
                return ApiResponse<ArticleResponseDto>.Fail("Article not found");

            article.ViewCount++;
            await _unitOfWork.Articles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ArticleResponseDto>.Ok(MapArticleToDto(article));
        }

        public async Task<ApiResponse<ArticleResponseDto>> GetArticleByIdAsync(Guid id)
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<ArticleResponseDto>.Fail("Article not found");

            var author = await _unitOfWork.Users.GetByIdAsync(article.AuthorId);
            article.Author = author;

            return ApiResponse<ArticleResponseDto>.Ok(MapArticleToDto(article));
        }

        public async Task<ApiResponse<PagedResponse<ArticleResponseDto>>> GetAllArticlesAsync(Guid? tenantId, PaginationParams pagination)
        {
            var all = await _unitOfWork.Articles.FindAsync(a => a.TenantId == tenantId);
            var totalCount = all.Count();
            var items = all
                .OrderByDescending(a => a.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .Select(MapArticleToDto)
                .ToList();
            var result = PagedResponse<ArticleResponseDto>.Create(items, totalCount, pagination);
            return ApiResponse<PagedResponse<ArticleResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<ArticleResponseDto>> UpdateArticleAsync(Guid id, CreateArticleDto dto)
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<ArticleResponseDto>.Fail("Article not found");

            article.Title = dto.Title;
            article.Content = dto.Content;
            article.Excerpt = dto.Excerpt;
            article.CoverImage = dto.CoverImage;
            article.MetaTitle = dto.MetaTitle;
            article.MetaDescription = dto.MetaDescription;
            article.Tags = dto.Tags;

            if (dto.IsPublished && !article.IsPublished)
                article.PublishedAt = DateTime.UtcNow;

            article.IsPublished = dto.IsPublished;

            await _unitOfWork.Articles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ArticleResponseDto>.Ok(MapArticleToDto(article), "Article updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteArticleAsync(Guid id)
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<bool>.Fail("Article not found");

            await _unitOfWork.Articles.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Article deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleArticlePublishAsync(Guid id)
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<bool>.Fail("Article not found");

            article.IsPublished = !article.IsPublished;
            if (article.IsPublished)
                article.PublishedAt = DateTime.UtcNow;

            await _unitOfWork.Articles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, article.IsPublished ? "Article published" : "Article unpublished");
        }

        private static PageResponseDto MapPageToDto(Page page) => new()
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            Content = page.Content,
            MetaTitle = page.MetaTitle,
            MetaDescription = page.MetaDescription,
            IsPublished = page.IsPublished,
            Type = page.Type,
            SortOrder = page.SortOrder,
            TenantId = page.TenantId,
            CreatedAt = page.CreatedAt
        };

        private static ArticleResponseDto MapArticleToDto(Article article) => new()
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Content = article.Content,
            Excerpt = article.Excerpt,
            CoverImage = article.CoverImage,
            MetaTitle = article.MetaTitle,
            MetaDescription = article.MetaDescription,
            IsPublished = article.IsPublished,
            PublishedAt = article.PublishedAt,
            Tags = article.Tags,
            ViewCount = article.ViewCount,
            TenantId = article.TenantId,
            AuthorId = article.AuthorId,
            AuthorName = article.Author != null
                ? $"{article.Author.FirstName} {article.Author.LastName}"
                : string.Empty,
            CreatedAt = article.CreatedAt
        };
    }
}