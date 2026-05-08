using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        // Staff وفوق — يشوف invoices الـ tenant
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _invoiceService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // Staff وفوق — يشوف invoice معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _invoiceService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // Staff وفوق — invoice الـ order المعين
        [HttpGet("order/{orderId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByOrderId(Guid orderId)
        {
            var result = await _invoiceService.GetByOrderIdAsync(orderId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — generate invoice من order
        [HttpPost("generate/{orderId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Generate(Guid orderId)
        {
            var result = await _invoiceService.GenerateFromOrderAsync(orderId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تغيير status الـ invoice
        [HttpPatch("{id}/status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] InvoiceStatus status)
        {
            var result = await _invoiceService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف invoice
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _invoiceService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}