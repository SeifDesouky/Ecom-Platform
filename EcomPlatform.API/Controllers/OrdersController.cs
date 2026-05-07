using EcomPlatform.Application.DTOs.Orders;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IAuditLogService _auditLogService;

        public OrdersController(IOrderService orderService, IAuditLogService auditLogService)
        {
            _orderService = orderService;
            _auditLogService = auditLogService;
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

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _auditLogService.LogAsync(
                "Order", result.Data!.Id.ToString(),
                AuditAction.Create, userId, dto.TenantId,
                newValue: $"Order {result.Data.OrderNumber} created",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");

            return Ok(result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OrderStatus status)
        {
            var order = await _orderService.GetByIdAsync(id);
            var oldStatus = order.Data?.Status.ToString() ?? "";

            var result = await _orderService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _auditLogService.LogAsync(
                "Order", id.ToString(),
                AuditAction.StatusChange, userId, order.Data?.TenantId,
                oldValue: oldStatus,
                newValue: status.ToString(),
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");

            return Ok(result);
        }

        [HttpPatch("{id}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] PaymentStatus status)
        {
            var order = await _orderService.GetByIdAsync(id);
            var oldStatus = order.Data?.PaymentStatus.ToString() ?? "";

            var result = await _orderService.UpdatePaymentStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _auditLogService.LogAsync(
                "Order", id.ToString(),
                AuditAction.StatusChange, userId, order.Data?.TenantId,
                oldValue: oldStatus,
                newValue: status.ToString(),
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");

            return Ok(result);
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _orderService.GetByIdAsync(id);

            var result = await _orderService.CancelOrderAsync(id);
            if (!result.Success)
                return BadRequest(result);

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _auditLogService.LogAsync(
                "Order", id.ToString(),
                AuditAction.StatusChange, userId, order.Data?.TenantId,
                oldValue: "Active",
                newValue: "Cancelled",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");

            return Ok(result);
        }
    }
}