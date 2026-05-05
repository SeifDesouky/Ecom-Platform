using EcomPlatform.Application.DTOs.Tickets;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _ticketService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _ticketService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _ticketService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTicketDto dto)
        {
            var result = await _ticketService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("reply")]
        public async Task<IActionResult> AddReply([FromBody] CreateTicketReplyDto dto)
        {
            var result = await _ticketService.AddReplyAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] TicketStatus status)
        {
            var result = await _ticketService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _ticketService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}