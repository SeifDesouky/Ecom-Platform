using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Products;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IAuditLogService _auditLogService;

        public ProductsController(
            IProductService productService,
            IAuditLogService auditLogService)
        {
            _productService = productService;
            _auditLogService = auditLogService;
        }

        // GET api/products?pageNumber=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams pagination)
        {
            var tenantId = GetTenantIdFromClaims();
            if (tenantId == null)
                return Unauthorized();

            var result = await _productService.GetAllByTenantAsync(tenantId.Value, pagination);
            return Ok(result);
        }

        // GET api/products/tenant/{tenantId} — SuperAdmin فقط
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllByTenant(
            Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _productService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // GET api/products/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _productService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // GET api/products/category/{categoryId}
        [HttpGet("category/{categoryId}")]
        public async Task<IActionResult> GetByCategory(
            Guid categoryId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _productService.GetByCategoryAsync(categoryId, pagination);
            return Ok(result);
        }

        // POST api/products
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
        {
            var result = await _productService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);

            await LogAudit("Product", result.Data!.Id.ToString(),
                AuditAction.Create, dto.TenantId,
                newValue: $"Product '{result.Data.Name}' created");

            return Ok(result);
        }

        // PUT api/products/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
        {
            var existing = await _productService.GetByIdAsync(id);
            var oldValue = existing.Data != null
                ? $"Name: {existing.Data.Name}, Price: {existing.Data.Price}"
                : "";

            var result = await _productService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);

            await LogAudit("Product", id.ToString(),
                AuditAction.Update, result.Data?.TenantId,
                oldValue: oldValue,
                newValue: $"Name: {result.Data?.Name}, Price: {result.Data?.Price}");

            return Ok(result);
        }

        // DELETE api/products/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _productService.GetByIdAsync(id);

            var result = await _productService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);

            await LogAudit("Product", id.ToString(),
                AuditAction.Delete, existing.Data?.TenantId,
                oldValue: $"Product '{existing.Data?.Name}' deleted");

            return Ok(result);
        }
        // DELETE api/products/bulk
        [HttpDelete("bulk")]
        public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids)
        {
            if (ids == null || ids.Count == 0)
                return BadRequest(new { Success = false, Message = "No IDs provided" });

            var tenantId = GetTenantIdFromClaims();
            if (tenantId == null)
                return Unauthorized();

            var deletedIds = new List<string>();
            var failedIds = new List<string>();

            foreach (var id in ids)
            {
                var existing = await _productService.GetByIdAsync(id);
                var result = await _productService.DeleteAsync(id);

                if (result.Success)
                {
                    deletedIds.Add(id.ToString());
                    await LogAudit("Product", id.ToString(),
                        AuditAction.Delete, tenantId,
                        oldValue: $"Product '{existing.Data?.Name}' bulk deleted");
                }
                else
                {
                    failedIds.Add(id.ToString());
                }
            }

            return Ok(new
            {
                Success = true,
                Data = new { Deleted = deletedIds.Count, Failed = failedIds.Count, FailedIds = failedIds }
            });
        }

        // PATCH api/products/{id}/toggle-status
        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var existing = await _productService.GetByIdAsync(id);
            var oldStatus = existing.Data?.Status.ToString() ?? "";

            var result = await _productService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);

            await LogAudit("Product", id.ToString(),
                AuditAction.StatusChange, existing.Data?.TenantId,
                oldValue: oldStatus,
                newValue: existing.Data?.IsActive == true ? "Inactive" : "Active");

            return Ok(result);
        }

        // PATCH api/products/{id}/stock
        [HttpPatch("{id}/stock")]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] int quantity)
        {
            var existing = await _productService.GetByIdAsync(id);
            var oldStock = existing.Data?.Stock.ToString() ?? "";

            var result = await _productService.UpdateStockAsync(id, quantity);
            if (!result.Success)
                return NotFound(result);

            await LogAudit("Product", id.ToString(),
                AuditAction.Update, existing.Data?.TenantId,
                oldValue: $"Stock: {oldStock}",
                newValue: $"Stock: {quantity}");

            return Ok(result);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid? GetTenantIdFromClaims()
        {
            var claim = User.FindFirstValue("tenantId");
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private Guid? GetUserIdFromClaims()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private async Task LogAudit(string entityName, string entityId,
            AuditAction action, Guid? tenantId,
            string oldValue = "", string newValue = "")
        {
            var userId = GetUserIdFromClaims();
            if (userId == null) return;

            await _auditLogService.LogAsync(
                entityName, entityId, action,
                userId.Value, tenantId,
                oldValue: oldValue,
                newValue: newValue,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");
        }
    }
}