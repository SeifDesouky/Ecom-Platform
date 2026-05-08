using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.CMS;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class CMSController : ControllerBase
    {
        private readonly ICMSService _cmsService;

        public CMSController(ICMSService cmsService)
        {
            _cmsService = cmsService;
        }

        // ─── Pages ────────────────────────────────────────────────────────────

        // AllowAnonymous — الـ storefront يقرأ الـ pages بدون login
        [HttpGet("pages/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllPages(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _cmsService.GetAllPagesAsync(tenantId, pagination);
            return Ok(result);
        }

        // AllowAnonymous — page معينة بالـ id
        [HttpGet("pages/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPageById(Guid id)
        {
            var result = await _cmsService.GetPageByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // AllowAnonymous — page بالـ slug (SEO-friendly URLs)
        [HttpGet("pages/slug/{slug}/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPageBySlug(string slug, Guid tenantId)
        {
            var result = await _cmsService.GetPageBySlugAsync(slug, tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إنشاء page
        [HttpPost("pages")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreatePage([FromBody] CreatePageDto dto)
        {
            var result = await _cmsService.CreatePageAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تعديل page
        [HttpPut("pages/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdatePage(Guid id, [FromBody] CreatePageDto dto)
        {
            var result = await _cmsService.UpdatePageAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف page
        [HttpDelete("pages/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeletePage(Guid id)
        {
            var result = await _cmsService.DeletePageAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — publish/unpublish page
        [HttpPatch("pages/{id}/toggle-publish")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> TogglePagePublish(Guid id)
        {
            var result = await _cmsService.TogglePagePublishAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // ─── Articles ─────────────────────────────────────────────────────────

        // AllowAnonymous — الـ blog عام للكل
        [HttpGet("articles/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllArticles(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _cmsService.GetAllArticlesAsync(tenantId, pagination);
            return Ok(result);
        }

        // AllowAnonymous — article معين
        [HttpGet("articles/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticleById(Guid id)
        {
            var result = await _cmsService.GetArticleByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // AllowAnonymous — article بالـ slug
        [HttpGet("articles/slug/{slug}/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticleBySlug(string slug, Guid tenantId)
        {
            var result = await _cmsService.GetArticleBySlugAsync(slug, tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — نشر article جديد
        [HttpPost("articles")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateArticle([FromBody] CreateArticleDto dto)
        {
            var result = await _cmsService.CreateArticleAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تعديل article
        [HttpPut("articles/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateArticle(Guid id, [FromBody] CreateArticleDto dto)
        {
            var result = await _cmsService.UpdateArticleAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف article
        [HttpDelete("articles/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteArticle(Guid id)
        {
            var result = await _cmsService.DeleteArticleAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — publish/unpublish article
        [HttpPatch("articles/{id}/toggle-publish")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleArticlePublish(Guid id)
        {
            var result = await _cmsService.ToggleArticlePublishAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}