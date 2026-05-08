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
    public class ZatcaController : ControllerBase
    {
        private readonly IZatcaService _zatcaService;

        public ZatcaController(IZatcaService zatcaService)
        {
            _zatcaService = zatcaService;
        }

        // TenantAdmin وفوق — generate ZATCA invoice لـ invoice معين
        [HttpGet("invoice/{invoiceId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GenerateZatcaInvoice(Guid invoiceId)
        {
            var result = await _zatcaService.GenerateZatcaInvoiceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — يجيب QR code كـ PNG
        [HttpGet("invoice/{invoiceId}/qr")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetQrCode(Guid invoiceId)
        {
            var result = await _zatcaService.GenerateZatcaInvoiceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);

            var qrBytes = Convert.FromBase64String(result.Data!.QrCodeBase64);
            return File(qrBytes, "image/png", $"zatca-qr-{invoiceId}.png");
        }
    }
}