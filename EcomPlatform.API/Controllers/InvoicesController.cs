using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        // Staff وفوق — يشوف invoices الـ tenant
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _invoiceService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // Staff وفوق — يشوف invoice معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _invoiceService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // Staff وفوق — invoice الـ order المعين
        [HttpGet("order/{orderId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByOrderId(Guid orderId)
        {
            var result = await _invoiceService.GetByOrderIdAsync(orderId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // صفحة تفاصيل الفاتورة مع الـ QR — بدون توكن
        [HttpGet("{id}/qr")]
        [AllowAnonymous]
        public async Task<IActionResult> GetQrCode(Guid id)
        {
            var result = await _invoiceService.GetByIdAsync(id);
            if (!result.Success || result.Data == null)
                return NotFound(result);

            var inv = result.Data;
            var qrBase64 = inv.QrCodeBase64 ?? "";

            var statusLabel = inv.Status switch
            {
                InvoiceStatus.Paid => "<span style='color:#16a34a;font-weight:600;'>&#10003; Paid</span>",
                InvoiceStatus.Unpaid => "<span style='color:#dc2626;font-weight:600;'>Unpaid</span>",
                _ => inv.Status.ToString()
            };

            // بناء صفوف المنتجات
            var itemsRows = new StringBuilder();
            foreach (var item in inv.Items)
            {
                itemsRows.Append("<tr>");
                itemsRows.Append("<td style='padding:10px 12px;border-bottom:1px solid #f0f0f0;'>" + item.Description + "</td>");
                itemsRows.Append("<td style='padding:10px 12px;border-bottom:1px solid #f0f0f0;text-align:center;'>" + item.Quantity + "</td>");
                itemsRows.Append("<td style='padding:10px 12px;border-bottom:1px solid #f0f0f0;text-align:right;'>" + item.UnitPrice.ToString("F2") + "</td>");
                itemsRows.Append("<td style='padding:10px 12px;border-bottom:1px solid #f0f0f0;text-align:right;font-weight:600;'>" + item.TotalPrice.ToString("F2") + "</td>");
                itemsRows.Append("</tr>");
            }

            var qrSection = string.IsNullOrEmpty(qrBase64)
                ? ""
                : "<div style='text-align:center;margin-top:32px;padding-top:24px;border-top:1px solid #f0f0f0;'>"
                  + "<p style='color:#888;font-size:12px;margin-bottom:12px;'>ZATCA QR Code</p>"
                  + "<div id='qr' style='display:inline-block;'></div>"
                  + "</div>";

            var qrScript = string.IsNullOrEmpty(qrBase64)
                ? ""
                : "<script src='https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js'></script>"
                  + "<script>new QRCode(document.getElementById('qr'),{text:'" + qrBase64 + "',width:180,height:180,colorDark:'#000000',colorLight:'#ffffff',correctLevel:QRCode.CorrectLevel.M});</script>";

            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html lang='ar' dir='rtl'><head>");
            html.Append("<meta charset='utf-8'>");
            html.Append("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            html.Append("<title>فاتورة " + inv.InvoiceNumber + "</title>");
            html.Append("<style>");
            html.Append("*{box-sizing:border-box;margin:0;padding:0;}");
            html.Append("body{font-family:'Segoe UI',Tahoma,sans-serif;background:#f0f2f5;color:#1a1a1a;padding:24px 16px;}");
            html.Append(".page{max-width:680px;margin:0 auto;}");
            html.Append(".card{background:white;border-radius:16px;padding:32px;box-shadow:0 4px 24px rgba(0,0,0,.08);margin-bottom:16px;}");
            html.Append(".header{display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:28px;padding-bottom:24px;border-bottom:2px solid #f0f0f0;}");
            html.Append(".inv-title{font-size:22px;font-weight:700;color:#1a1a1a;}");
            html.Append(".inv-number{font-size:13px;color:#888;margin-top:4px;}");
            html.Append(".badge{padding:6px 14px;border-radius:20px;font-size:13px;background:#f0fdf4;}");
            html.Append(".section-title{font-size:12px;color:#888;text-transform:uppercase;letter-spacing:.5px;margin-bottom:8px;font-weight:600;}");
            html.Append(".info-grid{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin-bottom:24px;}");
            html.Append(".info-block p{font-size:14px;color:#444;margin-top:4px;line-height:1.5;}");
            html.Append("table{width:100%;border-collapse:collapse;font-size:14px;}");
            html.Append("thead tr{background:#f8f9fa;}");
            html.Append("thead th{padding:10px 12px;text-align:right;font-size:12px;color:#888;font-weight:600;border-bottom:2px solid #f0f0f0;}");
            html.Append("thead th:nth-child(2){text-align:center;}");
            html.Append(".totals{margin-top:16px;border-top:2px solid #f0f0f0;padding-top:16px;}");
            html.Append(".total-row{display:flex;justify-content:space-between;padding:5px 0;font-size:14px;color:#555;}");
            html.Append(".total-row.grand{font-size:17px;font-weight:700;color:#1a1a1a;padding-top:10px;margin-top:6px;border-top:1px solid #e5e7eb;}");
            html.Append("@media(max-width:500px){.info-grid{grid-template-columns:1fr;}.header{flex-direction:column;gap:12px;}}");
            html.Append("</style></head><body><div class='page'>");

            // Card الفاتورة
            html.Append("<div class='card'>");

            // Header
            html.Append("<div class='header'>");
            html.Append("<div><div class='inv-title'>فاتورة ضريبية</div><div class='inv-number'>" + inv.InvoiceNumber + "</div><div class='inv-number'>تاريخ: " + inv.CreatedAt.ToString("yyyy-MM-dd") + "</div></div>");
            html.Append("<div class='badge'>" + statusLabel + "</div>");
            html.Append("</div>");

            // معلومات العميل والأوردر
            html.Append("<div class='info-grid'>");
            html.Append("<div class='info-block'><div class='section-title'>بيانات العميل</div>");
            html.Append("<p><strong>" + inv.CustomerName + "</strong></p>");
            html.Append("<p>" + inv.CustomerEmail + "</p>");
            html.Append("<p>" + inv.CustomerPhone + "</p>");
            html.Append("<p>" + inv.CustomerAddress + "</p>");
            html.Append("</div>");
            html.Append("<div class='info-block'><div class='section-title'>تفاصيل الفاتورة</div>");
            html.Append("<p>رقم الطلب: <strong>" + inv.OrderNumber + "</strong></p>");
            html.Append("<p>تاريخ الاستحقاق: " + inv.DueDate.ToString("yyyy-MM-dd") + "</p>");
            if (inv.PaidAt.HasValue)
                html.Append("<p>تاريخ الدفع: " + inv.PaidAt.Value.ToString("yyyy-MM-dd") + "</p>");
            html.Append("</div></div>");

            // جدول المنتجات
            html.Append("<div class='section-title'>المنتجات</div>");
            html.Append("<table><thead><tr>");
            html.Append("<th>المنتج</th><th style='text-align:center;'>الكمية</th><th>سعر الوحدة</th><th>الإجمالي</th>");
            html.Append("</tr></thead><tbody>");
            html.Append(itemsRows.ToString());
            html.Append("</tbody></table>");

            // الإجماليات
            html.Append("<div class='totals'>");
            html.Append("<div class='total-row'><span>المجموع الفرعي</span><span>" + inv.SubTotal.ToString("F2") + " SAR</span></div>");
            if (inv.Discount > 0)
                html.Append("<div class='total-row'><span>الخصم</span><span>- " + inv.Discount.ToString("F2") + " SAR</span></div>");
            html.Append("<div class='total-row'><span>الضريبة (15%)</span><span>" + inv.Tax.ToString("F2") + " SAR</span></div>");
            html.Append("<div class='total-row grand'><span>الإجمالي</span><span>" + inv.Total.ToString("F2") + " SAR</span></div>");
            html.Append("</div>");

            // QR Code
            html.Append(qrSection);
            html.Append("</div></div>");
            html.Append(qrScript);
            html.Append("</body></html>");

            return Content(html.ToString(), "text/html; charset=utf-8");
        }

        // TenantAdmin وفوق — generate invoice من order
        [HttpPost("generate/{orderId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Generate(Guid orderId)
        {
            var result = await _invoiceService.GenerateFromOrderAsync(orderId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تغيير status الـ invoice
        [HttpPatch("{id}/status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] InvoiceStatus status)
        {
            var result = await _invoiceService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف invoice
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _invoiceService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}