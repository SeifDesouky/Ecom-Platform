using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.HelpCenter;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using System.Text.RegularExpressions;

namespace EcomPlatform.Infrastructure.Services
{
    public class HelpCenterService : IHelpCenterService
    {
        private readonly IUnitOfWork _unitOfWork;

        public HelpCenterService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ── Slug Helper ───────────────────────────────────────────────────────
        private static string GenerateSlug(string value)
        {
            var slug = value.ToLower().Trim();
            slug = Regex.Replace(slug, @"[\s_]+", "-");
            slug = Regex.Replace(slug, @"[^a-z0-9-]", "");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }

        // ════════════════════════════════════════════════════════════════════
        // CATEGORIES
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<HelpCategoryResponseDto>> CreateCategoryAsync(CreateHelpCategoryDto dto)
        {
            // ✅ auto-generate الـ Slug لو جاء فاضي
            var slug = !string.IsNullOrWhiteSpace(dto.Slug)
                ? dto.Slug.ToLower().Trim()
                : GenerateSlug(dto.Name);

            var existing = await _unitOfWork.HelpCategories.FindAsync(
                c => c.Slug == slug && c.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<HelpCategoryResponseDto>.Fail("A category with this slug already exists");

            var category = new HelpCategory
            {
                Name = dto.Name,
                Slug = slug,
                Description = dto.Description,
                Icon = dto.Icon,
                SortOrder = dto.SortOrder,
                TenantId = dto.TenantId
            };

            await _unitOfWork.HelpCategories.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<HelpCategoryResponseDto>.Ok(MapCategory(category), "Category created successfully");
        }

        public async Task<ApiResponse<HelpCategoryResponseDto>> GetCategoryByIdAsync(Guid id)
        {
            var category = await _unitOfWork.HelpCategories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<HelpCategoryResponseDto>.Fail("Category not found");

            var articles = await _unitOfWork.HelpArticles.FindAsync(
                a => a.HelpCategoryId == id && a.Status == ArticleStatus.Published);
            category.Articles = articles.ToList();

            return ApiResponse<HelpCategoryResponseDto>.Ok(MapCategory(category));
        }

        public async Task<ApiResponse<HelpCategoryResponseDto>> GetCategoryBySlugAsync(string slug, Guid? tenantId)
        {
            var categories = await _unitOfWork.HelpCategories.FindAsync(
                c => c.Slug == slug && c.TenantId == tenantId && c.IsActive);
            var category = categories.FirstOrDefault();
            if (category == null)
                return ApiResponse<HelpCategoryResponseDto>.Fail("Category not found");

            var articles = await _unitOfWork.HelpArticles.FindAsync(
                a => a.HelpCategoryId == category.Id && a.Status == ArticleStatus.Published);
            category.Articles = articles.OrderBy(a => a.SortOrder).ToList();

            return ApiResponse<HelpCategoryResponseDto>.Ok(MapCategory(category));
        }

        public async Task<ApiResponse<List<HelpCategoryResponseDto>>> GetCategoriesAsync(Guid? tenantId)
        {
            var categories = await _unitOfWork.HelpCategories.FindAsync(
                c => c.TenantId == tenantId && c.IsActive);

            var result = new List<HelpCategoryResponseDto>();
            foreach (var cat in categories.OrderBy(c => c.SortOrder))
            {
                var articles = await _unitOfWork.HelpArticles.FindAsync(
                    a => a.HelpCategoryId == cat.Id && a.Status == ArticleStatus.Published);
                cat.Articles = articles.ToList();
                result.Add(MapCategory(cat));
            }

            return ApiResponse<List<HelpCategoryResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PagedResponse<HelpCategoryResponseDto>>> GetCategoriesAdminAsync(PaginationParams pagination)
        {
            var all = await _unitOfWork.HelpCategories.FindWithoutFilterAsync(_ => true);
            var totalCount = all.Count();
            var items = all
                .OrderBy(c => c.SortOrder)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToList();

            var result = PagedResponse<HelpCategoryResponseDto>.Create(
                items.Select(MapCategory).ToList(), totalCount, pagination);

            return ApiResponse<PagedResponse<HelpCategoryResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<HelpCategoryResponseDto>> UpdateCategoryAsync(Guid id, UpdateHelpCategoryDto dto)
        {
            var category = await _unitOfWork.HelpCategories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<HelpCategoryResponseDto>.Fail("Category not found");

            category.Name = dto.Name;
            category.Description = dto.Description;
            category.Icon = dto.Icon;
            category.SortOrder = dto.SortOrder;
            category.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.HelpCategories.UpdateAsync(category);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<HelpCategoryResponseDto>.Ok(MapCategory(category), "Category updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id)
        {
            var category = await _unitOfWork.HelpCategories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<bool>.Fail("Category not found");

            var articles = await _unitOfWork.HelpArticles.FindAsync(a => a.HelpCategoryId == id);
            if (articles.Any())
                return ApiResponse<bool>.Fail("Cannot delete category with existing articles");

            await _unitOfWork.HelpCategories.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Category deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleCategoryStatusAsync(Guid id)
        {
            var category = await _unitOfWork.HelpCategories.GetByIdAsync(id);
            if (category == null)
                return ApiResponse<bool>.Fail("Category not found");

            category.IsActive = !category.IsActive;
            category.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.HelpCategories.UpdateAsync(category);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, category.IsActive ? "Category activated" : "Category deactivated");
        }

        // ════════════════════════════════════════════════════════════════════
        // ARTICLES
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<HelpArticleResponseDto>> CreateArticleAsync(CreateHelpArticleDto dto)
        {
            var category = await _unitOfWork.HelpCategories.GetByIdAsync(dto.HelpCategoryId);
            if (category == null)
                return ApiResponse<HelpArticleResponseDto>.Fail("Category not found");

            // ✅ auto-generate الـ Slug لو جاء فاضي
            var slug = !string.IsNullOrWhiteSpace(dto.Slug)
                ? dto.Slug.ToLower().Trim()
                : GenerateSlug(dto.Title);

            var existing = await _unitOfWork.HelpArticles.FindAsync(
                a => a.Slug == slug && a.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<HelpArticleResponseDto>.Fail("An article with this slug already exists");

            var article = new HelpArticle
            {
                Title = dto.Title,
                Slug = slug,
                Content = dto.Content,
                Excerpt = dto.Excerpt,
                Tags = dto.Tags,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                IsFAQ = dto.IsFAQ,
                SortOrder = dto.SortOrder,
                Status = ArticleStatus.Draft,
                HelpCategoryId = dto.HelpCategoryId,
                AuthorId = dto.AuthorId,
                TenantId = dto.TenantId
            };

            await _unitOfWork.HelpArticles.AddAsync(article);
            await _unitOfWork.SaveChangesAsync();

            article.HelpCategory = category;
            return ApiResponse<HelpArticleResponseDto>.Ok(MapArticle(article), "Article created successfully");
        }

        public async Task<ApiResponse<HelpArticleResponseDto>> GetArticleByIdAsync(Guid id)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<HelpArticleResponseDto>.Fail("Article not found");

            await LoadArticleNavigationsAsync(article);
            return ApiResponse<HelpArticleResponseDto>.Ok(MapArticle(article));
        }

        public async Task<ApiResponse<HelpArticleResponseDto>> GetArticleBySlugAsync(string slug, Guid? tenantId)
        {
            var articles = await _unitOfWork.HelpArticles.FindAsync(
                a => a.Slug == slug && a.TenantId == tenantId && a.Status == ArticleStatus.Published);
            var article = articles.FirstOrDefault();
            if (article == null)
                return ApiResponse<HelpArticleResponseDto>.Fail("Article not found");

            await LoadArticleNavigationsAsync(article);
            return ApiResponse<HelpArticleResponseDto>.Ok(MapArticle(article));
        }

        // ✅ Public — Published فقط
        public async Task<ApiResponse<PagedResponse<HelpArticleResponseDto>>> GetArticlesByCategoryAsync(
            Guid categoryId, PaginationParams pagination)
        {
            var all = await _unitOfWork.HelpArticles.FindWithoutFilterAsync(
                a => a.HelpCategoryId == categoryId && a.Status == ArticleStatus.Published);

            var total = all.Count();
            var items = all
                .OrderBy(a => a.SortOrder)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToList();

            foreach (var item in items)
                await LoadArticleNavigationsAsync(item);

            var result = PagedResponse<HelpArticleResponseDto>.Create(
                items.Select(MapArticle).ToList(), total, pagination);
            return ApiResponse<PagedResponse<HelpArticleResponseDto>>.Ok(result);
        }

        // ✅ Admin — كل المقالات بدون فلتر Status (Draft + Published)
        public async Task<ApiResponse<PagedResponse<HelpArticleResponseDto>>> GetArticlesByCategoryAdminAsync(
            Guid categoryId, PaginationParams pagination)
        {
            var all = await _unitOfWork.HelpArticles.FindWithoutFilterAsync(
                a => a.HelpCategoryId == categoryId);

            var total = all.Count();
            var items = all
                .OrderBy(a => a.SortOrder)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToList();

            foreach (var item in items)
                await LoadArticleNavigationsAsync(item);

            var result = PagedResponse<HelpArticleResponseDto>.Create(
                items.Select(MapArticle).ToList(), total, pagination);
            return ApiResponse<PagedResponse<HelpArticleResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<List<HelpArticleResponseDto>>> GetFAQsAsync(Guid? tenantId)
        {
            var faqs = await _unitOfWork.HelpArticles.FindAsync(
                a => a.IsFAQ && a.TenantId == tenantId && a.Status == ArticleStatus.Published);

            foreach (var faq in faqs)
                await LoadArticleNavigationsAsync(faq);

            var sorted = faqs.OrderBy(a => a.SortOrder).ToList();
            return ApiResponse<List<HelpArticleResponseDto>>.Ok(sorted.Select(MapArticle).ToList());
        }

        public async Task<ApiResponse<HelpArticleResponseDto>> UpdateArticleAsync(Guid id, UpdateHelpArticleDto dto)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<HelpArticleResponseDto>.Fail("Article not found");

            article.Title = dto.Title;
            article.Content = dto.Content;
            article.Excerpt = dto.Excerpt;
            article.Tags = dto.Tags;
            article.MetaTitle = dto.MetaTitle;
            article.MetaDescription = dto.MetaDescription;
            article.IsFAQ = dto.IsFAQ;
            article.SortOrder = dto.SortOrder;
            article.HelpCategoryId = dto.HelpCategoryId;
            article.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.HelpArticles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            await LoadArticleNavigationsAsync(article);
            return ApiResponse<HelpArticleResponseDto>.Ok(MapArticle(article), "Article updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteArticleAsync(Guid id)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<bool>.Fail("Article not found");

            await _unitOfWork.HelpArticles.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Article deleted successfully");
        }

        public async Task<ApiResponse<bool>> PublishArticleAsync(Guid id)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<bool>.Fail("Article not found");

            article.Status = ArticleStatus.Published;
            article.PublishedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.HelpArticles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Article published successfully");
        }

        public async Task<ApiResponse<bool>> UnpublishArticleAsync(Guid id)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(id);
            if (article == null)
                return ApiResponse<bool>.Fail("Article not found");

            article.Status = ArticleStatus.Draft;
            article.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.HelpArticles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Article unpublished");
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ACTIONS
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<HelpSearchResultDto>> SearchAsync(string query, Guid? tenantId)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ApiResponse<HelpSearchResultDto>.Fail("Search query is required");

            var q = query.ToLower().Trim();

            var articles = await _unitOfWork.HelpArticles.FindAsync(
                a => a.TenantId == tenantId &&
                     a.Status == ArticleStatus.Published &&
                     (a.Title.ToLower().Contains(q) ||
                      a.Excerpt.ToLower().Contains(q) ||
                      a.Tags.ToLower().Contains(q)));

            var result = new HelpSearchResultDto
            {
                Query = query,
                TotalResults = articles.Count(),
                Articles = articles.OrderByDescending(a => a.ViewCount)
                                       .Select(a => new HelpArticleSummaryDto
                                       {
                                           Id = a.Id,
                                           Title = a.Title,
                                           Slug = a.Slug,
                                           Excerpt = a.Excerpt,
                                           IsFAQ = a.IsFAQ,
                                           ViewCount = a.ViewCount,
                                           HelpfulCount = a.HelpfulCount,
                                           SortOrder = a.SortOrder,
                                           PublishedAt = a.PublishedAt
                                       }).ToList()
            };

            return ApiResponse<HelpSearchResultDto>.Ok(result);
        }

        public async Task IncrementViewCountAsync(Guid articleId)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(articleId);
            if (article == null) return;

            article.ViewCount++;
            article.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.HelpArticles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ApiResponse<bool>> SubmitFeedbackAsync(Guid articleId, bool isHelpful)
        {
            var article = await _unitOfWork.HelpArticles.GetByIdAsync(articleId);
            if (article == null)
                return ApiResponse<bool>.Fail("Article not found");

            if (isHelpful) article.HelpfulCount++;
            else article.NotHelpfulCount++;
            article.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.HelpArticles.UpdateAsync(article);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Feedback submitted");
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private async Task LoadArticleNavigationsAsync(HelpArticle article)
        {
            article.HelpCategory = await _unitOfWork.HelpCategories.GetByIdAsync(article.HelpCategoryId);
            if (article.AuthorId.HasValue)
                article.Author = await _unitOfWork.Users.GetByIdAsync(article.AuthorId.Value);
        }

        private static HelpCategoryResponseDto MapCategory(HelpCategory c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            Icon = c.Icon,
            SortOrder = c.SortOrder,
            IsActive = c.IsActive,
            ArticlesCount = c.Articles?.Count ?? 0,
            TenantId = c.TenantId,
            CreatedAt = c.CreatedAt,
            Articles = c.Articles?.Where(a => a.Status == ArticleStatus.Published)
                                       .OrderBy(a => a.SortOrder)
                                       .Select(a => new HelpArticleSummaryDto
                                       {
                                           Id = a.Id,
                                           Title = a.Title,
                                           Slug = a.Slug,
                                           Excerpt = a.Excerpt,
                                           IsFAQ = a.IsFAQ,
                                           ViewCount = a.ViewCount,
                                           HelpfulCount = a.HelpfulCount,
                                           SortOrder = a.SortOrder,
                                           PublishedAt = a.PublishedAt
                                       }).ToList() ?? new()
        };

        private static HelpArticleResponseDto MapArticle(HelpArticle a) => new()
        {
            Id = a.Id,
            Title = a.Title,
            Slug = a.Slug,
            Content = a.Content,
            Excerpt = a.Excerpt,
            Tags = a.Tags,
            MetaTitle = a.MetaTitle,
            MetaDescription = a.MetaDescription,
            Status = a.Status,
            StatusName = a.Status.ToString(),
            IsFAQ = a.IsFAQ,
            SortOrder = a.SortOrder,
            ViewCount = a.ViewCount,
            HelpfulCount = a.HelpfulCount,
            NotHelpfulCount = a.NotHelpfulCount,
            HelpCategoryId = a.HelpCategoryId,
            HelpCategoryName = a.HelpCategory?.Name ?? string.Empty,
            AuthorName = a.Author != null
                                ? $"{a.Author.FirstName} {a.Author.LastName}".Trim()
                                : string.Empty,
            TenantId = a.TenantId,
            PublishedAt = a.PublishedAt,
            CreatedAt = a.CreatedAt
        };
    }
}