using EcomPlatform.Application.DTOs.Orders;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _orderService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _orderService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var result = await _orderService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OrderStatus status)
        {
            var result = await _orderService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] PaymentStatus status)
        {
            var result = await _orderService.UpdatePaymentStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var result = await _orderService.CancelOrderAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}