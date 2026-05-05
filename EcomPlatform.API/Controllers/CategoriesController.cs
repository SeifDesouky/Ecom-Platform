using EcomPlatform.Application.DTOs.Categories;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _categoryService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _categoryService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
        {
            var result = await _categoryService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryDto dto)
        {
            var result = await _categoryService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _categoryService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _categoryService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}