using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Accounting;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAccountingService
    {
        // ── Chart of Accounts ─────────────────────────────────────────────
        Task<ApiResponse<List<AccountResponseDto>>> GetChartOfAccountsAsync(Guid tenantId);
        Task<ApiResponse<AccountResponseDto>> GetAccountByIdAsync(Guid id);
        Task<ApiResponse<AccountResponseDto>> CreateAccountAsync(CreateAccountDto dto);
        Task<ApiResponse<bool>> ToggleAccountStatusAsync(Guid id);

        /// <summary>هيئ الحسابات الافتراضية لـ tenant جديد</summary>
        Task InitializeDefaultAccountsAsync(Guid tenantId);

        // ── Journal Entries ───────────────────────────────────────────────
        Task<ApiResponse<JournalEntryResponseDto>> CreateManualEntryAsync(CreateJournalEntryDto dto);
        Task<ApiResponse<JournalEntryResponseDto>> GetEntryByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<JournalEntryResponseDto>>> GetEntriesByTenantAsync(
            Guid tenantId, PaginationParams pagination, DateTime? from = null, DateTime? to = null);

        Task<ApiResponse<bool>> PostEntryAsync(Guid id);       // Draft → Posted
        Task<ApiResponse<bool>> ReverseEntryAsync(Guid id, Guid reversedById);

        // ── Auto-Journal Triggers ─────────────────────────────────────────
        Task CreateInvoicePaidEntryAsync(Guid invoiceId, Guid tenantId);
        Task CreateOrderPaidEntryAsync(Guid orderId, Guid tenantId);
        Task CreateRefundEntryAsync(Guid returnRequestId, Guid tenantId);
        Task CreateStockMovementEntryAsync(Guid stockMovementId, Guid tenantId);
        Task CreateSubscriptionPaidEntryAsync(Guid subscriptionId, Guid tenantId);

        // ── Reports ───────────────────────────────────────────────────────
        Task<ApiResponse<TrialBalanceDto>> GetTrialBalanceAsync(ReportFilterDto filter);
        Task<ApiResponse<ProfitAndLossDto>> GetProfitAndLossAsync(ReportFilterDto filter);
        Task<ApiResponse<BalanceSheetDto>> GetBalanceSheetAsync(ReportFilterDto filter);
        Task<ApiResponse<CashFlowDto>> GetCashFlowAsync(ReportFilterDto filter);
    }
}
