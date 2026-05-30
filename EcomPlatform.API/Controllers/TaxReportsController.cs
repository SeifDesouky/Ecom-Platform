using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.TaxReports;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/tax-reports")]
    [Authorize(Policy = Policies.TenantAdminOrAbove)]
    public class TaxReportsController : ControllerBase
    {
        private readonly ITaxReportService _taxReportService;
        private readonly IAuditLogService _auditLogService;

        public TaxReportsController(
            ITaxReportService taxReportService,
            IAuditLogService auditLogService)
        {
            _taxReportService = taxReportService;
            _auditLogService = auditLogService;
        }

        private Guid? GetUserId()
            => Guid.TryParse(User.FindFirstValue("userId"), out var id) ? id : null;
        private string GetIp()
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ════════════════════════════════════════════════════════════════
        // VAT SUMMARY
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/tax-reports/vat-summary?tenantId=xxx&dateFrom=2026-01-01&dateTo=2026-03-31
        /// <summary>
        /// ملخص VAT الكامل للفترة — يشمل التوزيع الشهري وسطور الفواتير.
        /// </summary>
        [HttpGet("vat-summary")]
        public async Task<IActionResult> GetVatSummary([FromQuery] TaxReportFilterDto filter)
        {
            if (filter.DateFrom > filter.DateTo)
                return BadRequest(ApiResponse<object>.Fail("DateFrom must be before DateTo."));

            var result = await _taxReportService.GetVatSummaryAsync(filter);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync(
                entityName: "TaxReport",
                entityId: filter.TenantId.ToString(),
                action: AuditAction.Read,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: filter.TenantId,
                newValue: $"VAT summary viewed: {filter.DateFrom:yyyy-MM-dd} → {filter.DateTo:yyyy-MM-dd}",
                ipAddress: GetIp());

            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // EXPORT
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/tax-reports/export/csv?tenantId=xxx&dateFrom=2026-01-01&dateTo=2026-03-31
        /// <summary>تصدير تقرير الضريبة بصيغة CSV</summary>
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportCsv([FromQuery] TaxReportFilterDto filter)
        {
            if (filter.DateFrom > filter.DateTo)
                return BadRequest(ApiResponse<object>.Fail("DateFrom must be before DateTo."));

            var bytes = await _taxReportService.ExportCsvAsync(filter);
            var fileName = $"vat-report-{filter.DateFrom:yyyyMMdd}-{filter.DateTo:yyyyMMdd}.csv";

            await _auditLogService.LogAsync(
                entityName: "TaxReport",
                entityId: filter.TenantId.ToString(),
                action: AuditAction.Export,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: filter.TenantId,
                newValue: $"CSV export: {filter.DateFrom:yyyy-MM-dd} → {filter.DateTo:yyyy-MM-dd}",
                ipAddress: GetIp());

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // GET /api/v1/tax-reports/export/excel?tenantId=xxx&dateFrom=2026-01-01&dateTo=2026-03-31
        /// <summary>تصدير تقرير الضريبة بصيغة Excel (.xls tab-separated)</summary>
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel([FromQuery] TaxReportFilterDto filter)
        {
            if (filter.DateFrom > filter.DateTo)
                return BadRequest(ApiResponse<object>.Fail("DateFrom must be before DateTo."));

            var bytes = await _taxReportService.ExportExcelAsync(filter);
            var fileName = $"vat-report-{filter.DateFrom:yyyyMMdd}-{filter.DateTo:yyyyMMdd}.xls";

            await _auditLogService.LogAsync(
                entityName: "TaxReport",
                entityId: filter.TenantId.ToString(),
                action: AuditAction.Export,
                userId: GetUserId() ?? Guid.Empty,
                tenantId: filter.TenantId,
                newValue: $"Excel export: {filter.DateFrom:yyyy-MM-dd} → {filter.DateTo:yyyy-MM-dd}",
                ipAddress: GetIp());

            return File(bytes, "application/vnd.ms-excel", fileName);
        }
    }
}
