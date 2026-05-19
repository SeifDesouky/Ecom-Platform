using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Zatca;
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

        // GET api/v1/Zatca/invoice/{invoiceId}
        [HttpGet("invoice/{invoiceId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GenerateZatcaInvoice(Guid invoiceId)
        {
            var result = await _zatcaService.GenerateZatcaInvoiceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // GET api/v1/Zatca/invoice/{invoiceId}/qr
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

        // POST api/v1/Zatca/invoice/{invoiceId}/submit
        [HttpPost("invoice/{invoiceId}/submit")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> SubmitInvoice(Guid invoiceId)
        {
            var result = await _zatcaService.SubmitInvoiceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // POST api/v1/Zatca/invoice/{invoiceId}/compliance
        [HttpPost("invoice/{invoiceId}/compliance")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CheckCompliance(Guid invoiceId)
        {
            var result = await _zatcaService.CheckComplianceAsync(invoiceId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // POST api/v1/Zatca/onboard
        [HttpPost("onboard")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Onboard([FromBody] ZatcaOnboardingRequestDto request)
        {
            var result = await _zatcaService.OnboardAsync(request);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // GET api/v1/Zatca/csr
        [HttpGet("csr")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public IActionResult GenerateCsr(
            [FromQuery] string commonName,
            [FromQuery] string organizationName,
            [FromQuery] string organizationalUnit,
            [FromQuery] string countryCode = "SA",
            [FromQuery] string vatNumber = "")
        {
            var result = _zatcaService.GenerateCsr(
                commonName, organizationName,
                organizationalUnit, countryCode, vatNumber);
            return Ok(result);
        }
    }
}