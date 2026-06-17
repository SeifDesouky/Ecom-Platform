using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.HelpCenter;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories([FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetCategoriesAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("categories/slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategoryBySlug(string slug, [FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetCategoryBySlugAsync(slug, tenantId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpGet("articles/slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticleBySlug(string slug, [FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetArticleBySlugAsync(slug, tenantId);
            if (!result.Success) return NotFound(result);

            _ = _helpCenterService.IncrementViewCountAsync(result.Data!.Id);
            return Ok(result);
        }

        [HttpGet("faqs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFAQs([FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.GetFAQsAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] Guid? tenantId)
        {
            var result = await _helpCenterService.SearchAsync(q, tenantId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("categories/{categoryId:guid}/articles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticlesByCategory(
            Guid categoryId, [FromQuery] PaginationParams pagination)
        {
            var result = await _helpCenterService.GetArticlesByCategoryAsync(categoryId, pagination);
            return Ok(result);
        }

        [HttpPost("articles/{id:guid}/feedback")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitFeedback(Guid id, [FromQuery] bool isHelpful)
        {
            var result = await _helpCenterService.SubmitFeedbackAsync(id, isHelpful);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // SUPER ADMIN
        // ════════════════════════════════════════════════════════════════════

        [HttpGet("admin/categories")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAllCategoriesAdmin([FromQuery] PaginationParams pagination)
        {
            var result = await _helpCenterService.GetCategoriesAdminAsync(pagination);
            return Ok(result);
        }

        // ✅ بيجيب كل المقالات بدون فلتر Status — Draft + Published
        [HttpGet("admin/categories/{categoryId:guid}/articles")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetArticlesByCategoryAdmin(
            Guid categoryId, [FromQuery] PaginationParams pagination)
        {
            var result = await _helpCenterService.GetArticlesByCategoryAdminAsync(categoryId, pagination);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // TENANT ADMIN
        // ════════════════════════════════════════════════════════════════════

        [HttpGet("categories/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetCategoryById(Guid id)
        {
            var result = await _helpCenterService.GetCategoryByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpPost("categories")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateHelpCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.CreateCategoryAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("categories/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateHelpCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.UpdateCategoryAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("categories/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var result = await _helpCenterService.DeleteCategoryAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("categories/{id:guid}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleCategory(Guid id)
        {
            var result = await _helpCenterService.ToggleCategoryStatusAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("articles/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetArticleById(Guid id)
        {
            var result = await _helpCenterService.GetArticleByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpPost("articles")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateArticle([FromBody] CreateHelpArticleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // ✅ خذ الـ AuthorId من الـ JWT تلقائياً
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var authorGuid))
                dto.AuthorId = authorGuid;

            var result = await _helpCenterService.CreateArticleAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("articles/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateArticle(Guid id, [FromBody] UpdateHelpArticleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpCenterService.UpdateArticleAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("articles/{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteArticle(Guid id)
        {
            var result = await _helpCenterService.DeleteArticleAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("articles/{id:guid}/publish")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> PublishArticle(Guid id)
        {
            var result = await _helpCenterService.PublishArticleAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("articles/{id:guid}/unpublish")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UnpublishArticle(Guid id)
        {
            var result = await _helpCenterService.UnpublishArticleAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}