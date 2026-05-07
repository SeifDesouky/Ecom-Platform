using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetTenantStats(Guid tenantId)
        {
            var result = await _dashboardService.GetTenantStatsAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("platform")]
        public async Task<IActionResult> GetPlatformStats()
        {
            var result = await _dashboardService.GetPlatformStatsAsync();
            return Ok(result);
        }

        [HttpGet("tenant/{tenantId}/snapshot")]
        public async Task<IActionResult> GetTenantSnapshot(Guid tenantId)
        {
            var result = await _dashboardService.GetLatestSnapshotAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("platform/snapshot")]
        public async Task<IActionResult> GetPlatformSnapshot()
        {
            var result = await _dashboardService.GetLatestSnapshotAsync(null);
            return Ok(result);
        }
    }
}