using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Tickets;
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
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        // SuperAdmin فقط — يشوف كل الـ tickets في المنصة
        [HttpGet]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams pagination)
        {
            var result = await _ticketService.GetAllAsync(pagination);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف tickets الـ tenant بتاعه
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _ticketService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف ticket معين بالتفاصيل والـ replies
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _ticketService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // AnyAuthenticatedUser — أي user يفتح ticket
        [HttpPost]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> Create([FromBody] CreateTicketDto dto)
        {
            var result = await _ticketService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تغيير status الـ ticket
        [HttpPatch("{id}/status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] TicketStatus status)
        {
            var result = await _ticketService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // AnyAuthenticatedUser — إضافة reply على ticket
        [HttpPost("{id}/reply")]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> AddReply(Guid id, [FromBody] CreateTicketReplyDto dto)
        {
            // ضمان إن الـ TicketId في الـ body مطابق لـ id في الـ URL
            dto.TicketId = id;
            var result = await _ticketService.AddReplyAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // SuperAdmin فقط — حذف ticket
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _ticketService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}