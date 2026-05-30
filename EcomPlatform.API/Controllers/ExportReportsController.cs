using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Accounting;
using EcomPlatform.Application.DTOs.ExportReports;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/export")]
    [Authorize(Policy = Policies.TenantAdminOrAbove)]
    public class ExportReportsController : ControllerBase
    {
        private readonly IExportReportService _exportService;
        private readonly IAuditLogService _auditLogService;

        public ExportReportsController(
            IExportReportService exportService,
            IAuditLogService auditLogService)
        {
            _exportService = exportService;
            _auditLogService = auditLogService;
        }

        private Guid? GetUserId()
            => Guid.TryParse(User.FindFirstValue("userId"), out var id) ? id : null;
        private string GetIp()
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ════════════════════════════════════════════════════════════════
        // ORDERS
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/export/orders?tenantId=xxx&format=csv&dateFrom=2026-01-01&dateTo=2026-03-31
        [HttpGet("orders")]
        public async Task<IActionResult> ExportOrders([FromQuery] OrdersExportFilterDto filter)
        {
            var result = await _exportService.ExportOrdersAsync(filter);
            if (result.Bytes.Length == 0)
                return BadRequest(ApiResponse<object>.Fail("No data found for the selected filters."));

            await LogExport("Orders", filter.TenantId, filter.Format, result.RowCount);
            return File(result.Bytes, result.ContentType, result.FileName);
        }

        // ════════════════════════════════════════════════════════════════
        // PRODUCTS
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/export/products?tenantId=xxx&format=excel&lowStockOnly=true
        [HttpGet("products")]
        public async Task<IActionResult> ExportProducts([FromQuery] ProductsExportFilterDto filter)
        {
            var result = await _exportService.ExportProductsAsync(filter);
            if (result.Bytes.Length == 0)
                return BadRequest(ApiResponse<object>.Fail("No data found for the selected filters."));

            await LogExport("Products", filter.TenantId, filter.Format, result.RowCount);
            return File(result.Bytes, result.ContentType, result.FileName);
        }

        // ════════════════════════════════════════════════════════════════
        // CUSTOMERS
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/export/customers?tenantId=xxx&format=pdf&activeOnly=true
        [HttpGet("customers")]
        public async Task<IActionResult> ExportCustomers([FromQuery] CustomersExportFilterDto filter)
        {
            var result = await _exportService.ExportCustomersAsync(filter);
            if (result.Bytes.Length == 0)
                return BadRequest(ApiResponse<object>.Fail("No data found for the selected filters."));

            await LogExport("Customers", filter.TenantId, filter.Format, result.RowCount);
            return File(result.Bytes, result.ContentType, result.FileName);
        }

        // ════════════════════════════════════════════════════════════════
        // INVENTORY
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/export/inventory?tenantId=xxx&format=excel
        [HttpGet("inventory")]
        public async Task<IActionResult> ExportInventory([FromQuery] ExportFilterDto filter)
        {
            var result = await _exportService.ExportInventoryAsync(filter);
            if (result.Bytes.Length == 0)
                return BadRequest(ApiResponse<object>.Fail("No data found."));

            await LogExport("Inventory", filter.TenantId, filter.Format, result.RowCount);
            return File(result.Bytes, result.ContentType, result.FileName);
        }

        // ════════════════════════════════════════════════════════════════
        // PRIVATE
        // ════════════════════════════════════════════════════════════════

        private async Task LogExport(string reportType, Guid tenantId, string format, int rows)
        {
            await _auditLogService.LogAsync(
                entityName: "ExportReport",
                entityId: tenantId.ToString(),
                action: AuditAction.Export,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: tenantId,
                newValue: $"{reportType} exported as {format.ToUpper()} — {rows} rows",
                ipAddress: GetIp());
        }
    }
}
