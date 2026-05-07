using EcomPlatform.Application.DTOs.CMS;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CMSController : ControllerBase
    {
        private readonly ICMSService _cmsService;

        public CMSController(ICMSService cmsService)
        {
            _cmsService = cmsService;
        }

        // ==================== PAGES ====================

        [HttpGet("pages/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllPages(Guid tenantId)
        {
            var result = await _cmsService.GetAllPagesAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("pages/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPageById(Guid id)
        {
            var result = await _cmsService.GetPageByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("pages/slug/{slug}/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPageBySlug(string slug, Guid tenantId)
        {
            var result = await _cmsService.GetPageBySlugAsync(slug, tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("pages")]
        [Authorize]
        public async Task<IActionResult> CreatePage([FromBody] CreatePageDto dto)
        {
            var result = await _cmsService.CreatePageAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("pages/{id}")]
        [Authorize]
        public async Task<IActionResult> UpdatePage(Guid id, [FromBody] CreatePageDto dto)
        {
            var result = await _cmsService.UpdatePageAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("pages/{id}")]
        [Authorize]
        public async Task<IActionResult> DeletePage(Guid id)
        {
            var result = await _cmsService.DeletePageAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("pages/{id}/toggle-publish")]
        [Authorize]
        public async Task<IActionResult> TogglePagePublish(Guid id)
        {
            var result = await _cmsService.TogglePagePublishAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // ==================== ARTICLES ====================

        [HttpGet("articles/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllArticles(Guid tenantId)
        {
            var result = await _cmsService.GetAllArticlesAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("articles/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticleById(Guid id)
        {
            var result = await _cmsService.GetArticleByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("articles/slug/{slug}/tenant/{tenantId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticleBySlug(string slug, Guid tenantId)
        {
            var result = await _cmsService.GetArticleBySlugAsync(slug, tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("articles")]
        [Authorize]
        public async Task<IActionResult> CreateArticle([FromBody] CreateArticleDto dto)
        {
            var result = await _cmsService.CreateArticleAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("articles/{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateArticle(Guid id, [FromBody] CreateArticleDto dto)
        {
            var result = await _cmsService.UpdateArticleAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("articles/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteArticle(Guid id)
        {
            var result = await _cmsService.DeleteArticleAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("articles/{id}/toggle-publish")]
        [Authorize]
        public async Task<IActionResult> ToggleArticlePublish(Guid id)
        {
            var result = await _cmsService.ToggleArticlePublishAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}