using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.TaxReports;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ITaxReportService
    {
        /// <summary>
        /// ملخص VAT الكامل لفترة زمنية — يشمل:
        /// إجمالي المبيعات، VAT المحصَّل، التوزيع الشهري، وسطور الفواتير.
        /// </summary>
        Task<ApiResponse<VatSummaryDto>> GetVatSummaryAsync(TaxReportFilterDto filter);

        /// <summary>تصدير التقرير CSV — يُرجع bytes جاهزة للـ File download</summary>
        Task<byte[]> ExportCsvAsync(TaxReportFilterDto filter);

        /// <summary>تصدير التقرير Excel (.xlsx)</summary>
        Task<byte[]> ExportExcelAsync(TaxReportFilterDto filter);
    }
}
