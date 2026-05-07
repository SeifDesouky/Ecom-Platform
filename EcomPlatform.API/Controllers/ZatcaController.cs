using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ZatcaController : ControllerBase
    {
        private readonly IZatcaService _zatcaService;

        public ZatcaController(IZatcaService zatcaService)
        {
            _zatcaService = zatcaService;
        }

        [HttpGet("invoice/{invoiceId}")]
        public async Task<IActionResult> GenerateZatcaInvoice(Guid invoiceId)
        {
            var result = await _zatcaService.GenerateZatcaInvoiceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("invoice/{invoiceId}/qr")]
        public async Task<IActionResult> GetQrCode(Guid invoiceId)
        {
            var result = await _zatcaService.GenerateZatcaInvoiceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);

            var qrBytes = Convert.FromBase64String(result.Data!.QrCodeBase64);
            return File(qrBytes, "image/png", $"qr-{invoiceId}.png");
        }
    }
}