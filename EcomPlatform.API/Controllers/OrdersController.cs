using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Orders;
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
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IAuditLogService _auditLogService;

        public OrdersController(IOrderService orderService, IAuditLogService auditLogService)
        {
            _orderService = orderService;
            _auditLogService = auditLogService;
        }

        // ─── Helper: قراءة userId من الـ JWT بأمان ──────────────────────────
        private Guid GetUserId() =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : throw new UnauthorizedAccessException();

        private string GetIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ─── GET /api/orders/tenant/{tenantId} ───────────────────────────────
        // Staff وفوق — يشوف orders الـ tenant
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _orderService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // ─── GET /api/orders/{id} ────────────────────────────────────────────
        // Staff وفوق — يشوف order معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _orderService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // ─── POST /api/orders ────────────────────────────────────────────────
        // Staff وفوق — إنشاء order جديد
        [HttpPost]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var result = await _orderService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "Order",
                entityId: result.Data!.Id.ToString(),
                action: AuditAction.Create,
                userId: GetUserId(),
                tenantId: dto.TenantId,
                newValue: $"Order {result.Data.OrderNumber} created",
                ipAddress: GetIp());

            return Ok(result);
        }

        // ─── PATCH /api/orders/{id}/status ──────────────────────────────────
        // Staff وفوق — تغيير حالة الـ order
        [HttpPatch("{id}/status")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OrderStatus status)
        {
            var order = await _orderService.GetByIdAsync(id);
            if (!order.Success)
                return NotFound(order);

            var oldStatus = order.Data!.Status.ToString();

            var result = await _orderService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "Order",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                userId: GetUserId(),
                tenantId: order.Data.TenantId,
                oldValue: oldStatus,
                newValue: status.ToString(),
                ipAddress: GetIp());

            return Ok(result);
        }

        // ─── PATCH /api/orders/{id}/payment-status ───────────────────────────
        // TenantAdmin وفوق — تغيير حالة الدفع (عملية حساسة)
        [HttpPatch("{id}/payment-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] PaymentStatus status)
        {
            var order = await _orderService.GetByIdAsync(id);
            if (!order.Success)
                return NotFound(order);

            var oldStatus = order.Data!.PaymentStatus.ToString();

            var result = await _orderService.UpdatePaymentStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "Order",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                userId: GetUserId(),
                tenantId: order.Data.TenantId,
                oldValue: oldStatus,
                newValue: status.ToString(),
                ipAddress: GetIp());

            return Ok(result);
        }

        // ─── PATCH /api/orders/{id}/cancel ───────────────────────────────────
        // TenantAdmin وفوق — إلغاء order (عملية حساسة)
        [HttpPatch("{id}/cancel")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _orderService.GetByIdAsync(id);
            if (!order.Success)
                return NotFound(order);

            var result = await _orderService.CancelOrderAsync(id);
            if (!result.Success)
                return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "Order",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                userId: GetUserId(),
                tenantId: order.Data!.TenantId,
                oldValue: order.Data.Status.ToString(),
                newValue: "Cancelled",
                ipAddress: GetIp());

            return Ok(result);
        }
    }
}