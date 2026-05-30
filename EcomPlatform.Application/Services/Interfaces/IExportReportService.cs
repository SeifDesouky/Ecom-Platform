using EcomPlatform.Application.DTOs.ExportReports;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IExportReportService
    {
        /// <summary>تصدير الطلبات — Orders</summary>
        Task<ExportResultDto> ExportOrdersAsync(OrdersExportFilterDto filter);

        /// <summary>تصدير المنتجات — Products</summary>
        Task<ExportResultDto> ExportProductsAsync(ProductsExportFilterDto filter);

        /// <summary>تصدير العملاء — Customers</summary>
        Task<ExportResultDto> ExportCustomersAsync(CustomersExportFilterDto filter);

        /// <summary>تصدير المخزون — Inventory</summary>
        Task<ExportResultDto> ExportInventoryAsync(ExportFilterDto filter);
    }
}
