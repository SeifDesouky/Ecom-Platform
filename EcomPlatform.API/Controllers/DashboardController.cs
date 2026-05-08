using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        // TenantAdmin وفوق — إحصائيات الـ tenant (real-time)
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetTenantStats(Guid tenantId)
        {
            var result = await _dashboardService.GetTenantStatsAsync(tenantId);
            return Ok(result);
        }

        // SuperAdmin فقط — إحصائيات المنصة كلها
        [HttpGet("platform")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetPlatformStats()
        {
            var result = await _dashboardService.GetPlatformStatsAsync();
            return Ok(result);
        }

        // TenantAdmin وفوق — آخر snapshot محفوظ للـ tenant
        [HttpGet("tenant/{tenantId}/snapshot")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetTenantSnapshot(Guid tenantId)
        {
            var result = await _dashboardService.GetLatestSnapshotAsync(tenantId);
            return Ok(result);
        }

        // SuperAdmin فقط — آخر snapshot للمنصة كلها
        [HttpGet("platform/snapshot")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetPlatformSnapshot()
        {
            var result = await _dashboardService.GetLatestSnapshotAsync(null);
            return Ok(result);
        }
    }
}