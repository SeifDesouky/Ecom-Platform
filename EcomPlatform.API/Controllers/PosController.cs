using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Pos;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/pos")]
    [Authorize]
    public class PosController : ControllerBase
    {
        private readonly IPosService _posService;
        private readonly IAuditLogService _auditLogService;

        public PosController(IPosService posService, IAuditLogService auditLogService)
        {
            _posService = posService;
            _auditLogService = auditLogService;
        }

        private Guid GetUserId() =>
            Guid.TryParse(User.FindFirstValue("userId"), out var id)
                ? id
                : throw new UnauthorizedAccessException();

        private string GetIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ════════════════════════════════════════════════════════════════
        // SESSIONS
        // ════════════════════════════════════════════════════════════════

        // POST /api/v1/pos/sessions/open
        [HttpPost("sessions/open")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> OpenSession([FromBody] OpenPosSessionDto dto)
        {
            var cashierId = GetUserId();
            var result = await _posService.OpenSessionAsync(dto, cashierId);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "PosSession",
                entityId: result.Data!.Id.ToString(),
                action: AuditAction.Create,
                userId: cashierId,
                tenantId: dto.TenantId,
                newValue: $"POS session opened on terminal {dto.TerminalName}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // POST /api/v1/pos/sessions/{id}/close
        [HttpPost("sessions/{id}/close")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> CloseSession(Guid id, [FromBody] ClosePosSessionDto dto)
        {
            var cashierId = GetUserId();
            var result = await _posService.CloseSessionAsync(id, dto, cashierId);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "PosSession",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                userId: cashierId,
                tenantId: null,
                oldValue: "Open",
                newValue: "Closed",
                ipAddress: GetIp());

            return Ok(result);
        }

        // GET /api/v1/pos/sessions/active?tenantId=xxx
        [HttpGet("sessions/active")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetActiveSession([FromQuery] Guid tenantId)
        {
            // ✅ FIX: رجّع Ok دايماً عشان الفرونت يقدر يقرأ الـ response
            // لو مفيش session، الـ success=false والفرونت يتعامل معاها في next: مش error:
            var result = await _posService.GetActiveSessionAsync(tenantId, GetUserId());
            return Ok(result);
        }

        // GET /api/v1/pos/sessions?tenantId=xxx
        [HttpGet("sessions")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetSessions(
            [FromQuery] Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _posService.GetSessionsAsync(tenantId, pagination);
            return Ok(result);
        }

        // GET /api/v1/pos/sessions/{id}
        [HttpGet("sessions/{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetSessionById(Guid id)
        {
            var result = await _posService.GetSessionByIdAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // ORDERS / SALES
        // ════════════════════════════════════════════════════════════════

        // POST /api/v1/pos/orders
        [HttpPost("orders")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> CreateOrder([FromBody] CreatePosOrderDto dto)
        {
            var cashierId = GetUserId();
            var result = await _posService.CreateOrderAsync(dto, cashierId);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "PosOrder",
                entityId: result.Data!.Id.ToString(),
                action: AuditAction.Create,
                userId: cashierId,
                tenantId: dto.TenantId,
                newValue: $"POS sale {result.Data.ReceiptNumber} — Total: {result.Data.Total}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // PATCH /api/v1/pos/orders/{id}/void
        [HttpPatch("orders/{id}/void")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> VoidOrder(Guid id, [FromBody] VoidPosOrderDto dto)
        {
            var cashierId = GetUserId();
            var result = await _posService.VoidOrderAsync(id, dto, cashierId);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "PosOrder",
                entityId: id.ToString(),
                action: AuditAction.StatusChange,
                userId: cashierId,
                tenantId: null,
                oldValue: "Completed",
                newValue: $"Voided — {dto.Reason}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // GET /api/v1/pos/orders/{id}/receipt
        [HttpGet("orders/{id}/receipt")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetReceipt(Guid id)
        {
            var result = await _posService.GetOrderReceiptAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // GET /api/v1/pos/sessions/{sessionId}/orders
        [HttpGet("sessions/{sessionId}/orders")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetSessionOrders(Guid sessionId)
        {
            var result = await _posService.GetSessionOrdersAsync(sessionId);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // PRODUCTS (Quick Search for POS screen)
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/pos/products/search?tenantId=xxx&q=cola
        [HttpGet("products/search")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] Guid tenantId,
            [FromQuery] string q)
        {
            var result = await _posService.SearchProductsAsync(tenantId, q);
            return Ok(result);
        }

        // GET /api/v1/pos/products/barcode/{barcode}?tenantId=xxx
        [HttpGet("products/barcode/{barcode}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByBarcode(
            string barcode,
            [FromQuery] Guid tenantId)
        {
            var result = await _posService.GetProductByBarcodeAsync(tenantId, barcode);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }
    }
}