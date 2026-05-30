using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.PaymentLinks;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/v1/payment-links")]
    public class PaymentLinksController : ControllerBase
    {
        private readonly IPaymentLinkService _paymentLinkService;

        public PaymentLinksController(IPaymentLinkService paymentLinkService)
        {
            _paymentLinkService = paymentLinkService;
        }

        // ════════════════════════════════════════════════════════════════════
        // CRUD — محتاج Auth
        // ════════════════════════════════════════════════════════════════════

        /// <summary>جلب كل روابط الدفع للـ tenant</summary>
        [HttpGet("tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAll(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _paymentLinkService.GetByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        /// <summary>جلب رابط بالـ ID</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _paymentLinkService.GetByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>جلب رابط بالـ Code (Admin)</summary>
        [HttpGet("code/{code}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByCode(string code)
        {
            var result = await _paymentLinkService.GetByCodeAsync(code);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>إنشاء رابط دفع جديد</summary>
        [HttpPost]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreatePaymentLinkDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _paymentLinkService.CreateAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تعديل رابط دفع</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePaymentLinkDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _paymentLinkService.UpdateAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>حذف رابط دفع</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _paymentLinkService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تفعيل رابط</summary>
        [HttpPatch("{id:guid}/activate")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Activate(Guid id)
        {
            var result = await _paymentLinkService.ActivateAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>إيقاف رابط</summary>
        [HttpPatch("{id:guid}/deactivate")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var result = await _paymentLinkService.DeactivateAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // TRANSACTIONS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>معاملات رابط معين</summary>
        [HttpGet("{id:guid}/transactions")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetTransactions(Guid id, [FromQuery] PaginationParams pagination)
        {
            var result = await _paymentLinkService.GetTransactionsAsync(id, pagination);
            return Ok(result);
        }

        /// <summary>كل معاملات الـ tenant</summary>
        [HttpGet("transactions/tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllTransactions(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _paymentLinkService.GetTransactionsByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC — بدون Auth (يُستخدم في صفحة الدفع العامة)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// جلب معلومات الرابط للعرض العام — لا يحتاج auth.
        /// يُستخدم من صفحة /pay/{code} في الفرونت.
        /// </summary>
        [HttpGet("public/{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicInfo(string code)
        {
            var result = await _paymentLinkService.GetPublicInfoAsync(code);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// تسجيل دفعة على رابط — يُستدعى بعد تأكيد البوابة.
        /// بدون Auth لأن العميل الدافع ممكن مش مسجل.
        /// </summary>
        [HttpPost("public/{code}/pay")]
        [AllowAnonymous]
        public async Task<IActionResult> ProcessPayment(string code, [FromBody] ProcessPaymentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            dto.LinkCode = code;    // override من الـ route
            var result = await _paymentLinkService.ProcessPaymentAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
