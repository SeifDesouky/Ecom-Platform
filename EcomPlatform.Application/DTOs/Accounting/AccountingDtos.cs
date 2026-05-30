using EcomPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Accounting
{
    // ══════════════════════════════════════════════════════════════════════
    // CHART OF ACCOUNTS
    // ══════════════════════════════════════════════════════════════════════

    public class CreateAccountDto
    {
        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [Required]
        public AccountType Type { get; set; }

        [Required]
        public AccountNature Nature { get; set; }

        public Guid? ParentId { get; set; }

        [Required]
        public Guid TenantId { get; set; }
    }

    public class AccountResponseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public AccountNature Nature { get; set; }
        public string NatureName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
        public Guid? ParentId { get; set; }
        public string? ParentName { get; set; }
        public decimal Balance { get; set; }   // الرصيد الحالي
        public List<AccountResponseDto> Children { get; set; } = new();
    }

    // ══════════════════════════════════════════════════════════════════════
    // JOURNAL ENTRIES
    // ══════════════════════════════════════════════════════════════════════

    public class CreateJournalEntryDto
    {
        public DateTime EntryDate { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        [Required, MinLength(2)]
        public List<CreateJournalEntryLineDto> Lines { get; set; } = new();

        [Required]
        public Guid TenantId { get; set; }
        public Guid? CreatedById { get; set; }
    }

    public class CreateJournalEntryLineDto
    {
        [Required]
        public Guid AccountId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Debit { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Credit { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    public class JournalEntryResponseDto
    {
        public Guid Id { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public JournalEntrySource Source { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public JournalEntryStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public Guid? ReferenceId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<JournalEntryLineDto> Lines { get; set; } = new();
    }

    public class JournalEntryLineDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════════
    // REPORTS
    // ══════════════════════════════════════════════════════════════════════

    public class ReportFilterDto
    {
        [Required]
        public Guid TenantId { get; set; }

        public DateTime FromDate { get; set; } = new DateTime(DateTime.UtcNow.Year, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.UtcNow;
    }

    // ── Trial Balance ──────────────────────────────────────────────────────

    public class TrialBalanceDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<TrialBalanceLineDto> Lines { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public bool IsBalanced => TotalDebit == TotalCredit;
    }

    public class TrialBalanceLineDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }   // Debit - Credit
    }

    // ── Profit & Loss ──────────────────────────────────────────────────────

    public class ProfitAndLossDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public List<PLSectionDto> Revenue { get; set; } = new();
        public List<PLSectionDto> Expenses { get; set; } = new();

        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetProfitMargin { get; set; }   // نسبة مئوية
    }

    public class PLSectionDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    // ── Balance Sheet ──────────────────────────────────────────────────────

    public class BalanceSheetDto
    {
        public DateTime AsOfDate { get; set; }

        public List<BSLineDto> Assets { get; set; } = new();
        public List<BSLineDto> Liabilities { get; set; } = new();
        public List<BSLineDto> Equity { get; set; } = new();

        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public bool IsBalanced => TotalAssets == TotalLiabilities + TotalEquity;
    }

    public class BSLineDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    // ── Cash Flow ─────────────────────────────────────────────────────────

    public class CashFlowDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public decimal OperatingCashFlow { get; set; }
        public decimal InvestingCashFlow { get; set; }
        public decimal FinancingCashFlow { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }

        public List<CashFlowLineDto> OperatingLines { get; set; } = new();
        public List<CashFlowLineDto> InvestingLines { get; set; } = new();
        public List<CashFlowLineDto> FinancingLines { get; set; } = new();
    }

    public class CashFlowLineDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
