using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Accounting;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/v1/accounting")]
    [Authorize(Policy = Policies.TenantAdminOrAbove)]
    public class AccountingController : ControllerBase
    {
        private readonly IAccountingService _accountingService;

        public AccountingController(IAccountingService accountingService)
        {
            _accountingService = accountingService;
        }

        // ════════════════════════════════════════════════════════════════════
        // CHART OF ACCOUNTS
        // ════════════════════════════════════════════════════════════════════

        [HttpGet("accounts/{tenantId:guid}")]
        public async Task<IActionResult> GetChartOfAccounts(Guid tenantId)
            => Ok(await _accountingService.GetChartOfAccountsAsync(tenantId));

        [HttpGet("accounts/detail/{id:guid}")]
        public async Task<IActionResult> GetAccount(Guid id)
        {
            var result = await _accountingService.GetAccountByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // جيب TenantId من الـ header أو JWT بدل ما تأخده من الـ body
            var tenantValue = HttpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault()
                           ?? User.FindFirst("tenantId")?.Value;

            if (string.IsNullOrEmpty(tenantValue) || !Guid.TryParse(tenantValue, out var tenantId))
                return Unauthorized();

            dto.TenantId = tenantId;

            var result = await _accountingService.CreateAccountAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("accounts/{id:guid}")]
        public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _accountingService.UpdateAccountAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("accounts/{id:guid}/toggle")]
        public async Task<IActionResult> ToggleAccount(Guid id)
        {
            var result = await _accountingService.ToggleAccountStatusAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("accounts/{tenantId:guid}/initialize")]
        public async Task<IActionResult> InitializeAccounts(Guid tenantId)
        {
            await _accountingService.InitializeDefaultAccountsAsync(tenantId);
            return Ok(new { success = true, message = "Default accounts initialized" });
        }

        // ════════════════════════════════════════════════════════════════════
        // JOURNAL ENTRIES
        // ════════════════════════════════════════════════════════════════════

        [HttpGet("entries/{tenantId:guid}")]
        public async Task<IActionResult> GetEntries(
            Guid tenantId,
            [FromQuery] PaginationParams pagination,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
            => Ok(await _accountingService.GetEntriesByTenantAsync(tenantId, pagination, from, to));

        [HttpGet("entries/detail/{id:guid}")]
        public async Task<IActionResult> GetEntry(Guid id)
        {
            var result = await _accountingService.GetEntryByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpPost("entries")]
        public async Task<IActionResult> CreateEntry([FromBody] CreateJournalEntryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _accountingService.CreateManualEntryAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("entries/{id:guid}/post")]
        public async Task<IActionResult> PostEntry(Guid id)
        {
            var result = await _accountingService.PostEntryAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("entries/{id:guid}/reverse")]
        public async Task<IActionResult> ReverseEntry(Guid id, [FromQuery] Guid reversedById)
        {
            var result = await _accountingService.ReverseEntryAsync(id, reversedById);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // REPORTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>ميزان المراجعة</summary>
        [HttpGet("reports/trial-balance")]
        public async Task<IActionResult> TrialBalance([FromQuery] ReportFilterDto filter)
            => Ok(await _accountingService.GetTrialBalanceAsync(filter));

        /// <summary>قائمة الأرباح والخسائر</summary>
        [HttpGet("reports/profit-and-loss")]
        public async Task<IActionResult> ProfitAndLoss([FromQuery] ReportFilterDto filter)
            => Ok(await _accountingService.GetProfitAndLossAsync(filter));

        /// <summary>الميزانية العمومية</summary>
        [HttpGet("reports/balance-sheet")]
        public async Task<IActionResult> BalanceSheet([FromQuery] ReportFilterDto filter)
            => Ok(await _accountingService.GetBalanceSheetAsync(filter));

        /// <summary>قائمة التدفقات النقدية</summary>
        [HttpGet("reports/cash-flow")]
        public async Task<IActionResult> CashFlow([FromQuery] ReportFilterDto filter)
            => Ok(await _accountingService.GetCashFlowAsync(filter));
    }
}