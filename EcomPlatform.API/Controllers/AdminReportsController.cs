using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.AdminReports;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/v1/admin/reports")]
    [Authorize(Policy = Policies.SuperAdminOnly)]
    public class AdminReportsController : ControllerBase
    {
        private readonly IAdminReportService _reportService;

        public AdminReportsController(IAdminReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>تقرير المتاجر — عدد، نمو، أداء كل متجر</summary>
        [HttpGet("stores")]
        public async Task<IActionResult> GetStoresReport([FromQuery] ReportQueryParams query)
        {
            var result = await _reportService.GetStoresReportAsync(query);
            return Ok(result);
        }

        /// <summary>تقرير الإيرادات — إجمالي، شهري، أفضل متاجر</summary>
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenueReport([FromQuery] ReportQueryParams query)
        {
            var result = await _reportService.GetRevenueReportAsync(query);
            return Ok(result);
        }

        /// <summary>تقرير الأوردرات — إجمالي، حالات، نسب</summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrdersReport([FromQuery] ReportQueryParams query)
        {
            var result = await _reportService.GetOrdersReportAsync(query);
            return Ok(result);
        }

        /// <summary>تقرير الاشتراكات — MRR، ARR، churn، باقات، منتهية قريباً</summary>
        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptionsReport([FromQuery] ReportQueryParams query)
        {
            var result = await _reportService.GetSubscriptionsReportAsync(query);
            return Ok(result);
        }
    }
}
