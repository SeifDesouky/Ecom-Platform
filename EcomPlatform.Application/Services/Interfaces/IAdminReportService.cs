using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.AdminReports;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAdminReportService
    {
        Task<ApiResponse<StoresReportDto>> GetStoresReportAsync(ReportQueryParams query);
        Task<ApiResponse<RevenueReportDto>> GetRevenueReportAsync(ReportQueryParams query);
        Task<ApiResponse<OrdersReportDto>> GetOrdersReportAsync(ReportQueryParams query);
        Task<ApiResponse<SubscriptionsReportDto>> GetSubscriptionsReportAsync(ReportQueryParams query);
    }
}
