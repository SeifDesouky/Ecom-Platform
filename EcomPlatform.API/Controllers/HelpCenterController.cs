using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.HelpCenter;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/v1/help")]
    public class HelpCenterController : ControllerBase
    {
        private readonly IHelpCenterService _helpCenterService;
        public HelpCenterController(IHelpCenterService helpCenterService)
            => _helpCenterService = helpCenterService;

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC — بدون login
        // ════════════════════════════════════════════════════════════════════

        /// <summary>جلب كل التصنيفات مع مقالاتها المنشورة</summary>
        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories([FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetCategoriesAsync(tenantId);
            return Ok(result);
        }

        /// <summary>جلب تصنيف بالـ slug</summary>
        [HttpGet("categories/slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategoryBySlug(string slug, [FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetCategoryBySlugAsync(slug, tenantId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>جلب مقالة بالـ slug</summary>
        [HttpGet("articles/slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticleBySlug(string slug, [FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetArticleBySlugAsync(slug, tenantId);
            if (!result.Success) return NotFound(result);

            _ = _helpCenterService.IncrementViewCountAsync(result.Data!.Id);
            return Ok(result);
        }

        /// <summary>جلب الـ FAQs</summary>
        [HttpGet("faqs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFAQs([FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetFAQsAsync(tenantId);
            return Ok(result);
        }

        /// <summary>البحث في مركز المساعدة</summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.SearchAsync(q, tenantId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>مقالات تصنيف معين — للـ storefront</summary>
        [HttpGet("categories/{categoryId:guid}/articles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticlesByCategory(
            Guid categoryId, [FromQuery] PaginationParams pagination)
        {
            var result = await _helpCenterService.GetArticlesByCategoryAsync(categoryId, pagination);
            return Ok(result);
        }

        /// <summary>تقييم مقالة (مفيدة / غير مفيدة)</summary>
        [HttpPost("articles/{id:guid}/feedback")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitFeedback(Guid id, [FromQuery] bool isHelpful)
        {
            var result = await _helpCenterService.SubmitFeedbackAsync(id, isHelpful);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // SUPER ADMIN — بدون tenantId
        // ════════════════════════════════════════════════════════════════════

        /// <summary>جلب كل التصنيفات — للسوبر ادمن</summary>
        [HttpGet("admin/categories")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAllCategoriesAdmin([FromQuery] PaginationParams pagination)
        {
            var result = await _helpCenterService.GetCategoriesAdminAsync(pagination);
            return Ok(result);
        }

        /// <summary>جلب مقالات تصنيف — للسوبر ادمن</summary>
        [HttpGet("admin/categories/{categoryId:guid}/articles")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetArticlesByCategoryAdmin(
            Guid categoryId, [FromQuery] PaginationParams pagination)
        {
            var result = await _helpCenterService.GetArticlesByCategoryAsync(categoryId, pagination);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // TENANT ADMIN — TenantAdmin+
        // ════════════════════════════════════════════════════════════════════

        /// <summary>جلب تصنيف بالـ ID</summary>
        [HttpGet("categories/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetCategoryById(Guid id)
        {
            var result = await _helpCenterService.GetCategoryByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>إنشاء تصنيف</summary>
        [HttpPost("categories")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateHelpCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.CreateCategoryAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تعديل تصنيف</summary>
        [HttpPut("categories/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateHelpCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.UpdateCategoryAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>حذف تصنيف</summary>
        [HttpDelete("categories/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var result = await _helpCenterService.DeleteCategoryAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تفعيل / تعطيل تصنيف</summary>
        [HttpPatch("categories/{id:guid}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleCategory(Guid id)
        {
            var result = await _helpCenterService.ToggleCategoryStatusAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>جلب مقالة بالـ ID</summary>
        [HttpGet("articles/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetArticleById(Guid id)
        {
            var result = await _helpCenterService.GetArticleByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>إنشاء مقالة</summary>
        [HttpPost("articles")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateArticle([FromBody] CreateHelpArticleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.CreateArticleAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تعديل مقالة</summary>
        [HttpPut("articles/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateArticle(Guid id, [FromBody] UpdateHelpArticleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.UpdateArticleAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>حذف مقالة</summary>
        [HttpDelete("articles/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteArticle(Guid id)
        {
            var result = await _helpCenterService.DeleteArticleAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>نشر مقالة</summary>
        [HttpPatch("articles/{id:guid}/publish")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> PublishArticle(Guid id)
        {
            var result = await _helpCenterService.PublishArticleAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>إلغاء نشر مقالة</summary>
        [HttpPatch("articles/{id:guid}/unpublish")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UnpublishArticle(Guid id)
        {
            var result = await _helpCenterService.UnpublishArticleAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}