using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Accounting;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class AccountingService : IAccountingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogService _auditLogService;

        private const string CASH = "1100";
        private const string ACCOUNTS_RECEIVABLE = "1200";
        private const string INVENTORY = "1300";
        private const string ACCOUNTS_PAYABLE = "2100";
        private const string SALES_REVENUE = "4100";
        private const string REFUNDS_EXPENSE = "4200";
        private const string COGS = "5100";
        private const string SUBSCRIPTION_REV = "4300";
        private const string STOCK_LOSS = "5200";
        private const string RETAINED_EARNINGS = "3200";
        private const string OWNER_EQUITY = "3100";

        public AccountingService(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
        {
            _unitOfWork = unitOfWork;
            _auditLogService = auditLogService;
        }

        // ════════════════════════════════════════════════════════════════════
        // CHART OF ACCOUNTS
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<List<AccountResponseDto>>> GetChartOfAccountsAsync(Guid tenantId)
        {
            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == tenantId);
            var balances = await GetAccountBalancesAsync(tenantId);

            var allAccounts = accounts.OrderBy(a => a.Code).ToList();
            var roots = allAccounts.Where(a => a.ParentId == null)
                                   .Select(a => MapAccount(a, allAccounts, balances))
                                   .ToList();

            return ApiResponse<List<AccountResponseDto>>.Ok(roots);
        }

        public async Task<ApiResponse<AccountResponseDto>> GetAccountByIdAsync(Guid id)
        {
            var account = await _unitOfWork.ChartOfAccounts.GetByIdAsync(id);
            if (account == null) return ApiResponse<AccountResponseDto>.Fail("Account not found");

            var balances = await GetAccountBalancesAsync(account.TenantId ?? Guid.Empty);
            var all = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == account.TenantId);
            return ApiResponse<AccountResponseDto>.Ok(MapAccount(account, all.ToList(), balances));
        }

        public async Task<ApiResponse<AccountResponseDto>> CreateAccountAsync(CreateAccountDto dto)
        {
            var exists = await _unitOfWork.ChartOfAccounts.FindAsync(
                a => a.TenantId == dto.TenantId && a.Code == dto.Code);
            if (exists.Any())
                return ApiResponse<AccountResponseDto>.Fail($"Account code '{dto.Code}' already exists");

            ChartOfAccount? parent = null;
            if (dto.ParentId.HasValue)
            {
                parent = await _unitOfWork.ChartOfAccounts.GetByIdAsync(dto.ParentId.Value);
                if (parent == null)
                    return ApiResponse<AccountResponseDto>.Fail("Parent account not found");
            }

            var account = new ChartOfAccount
            {
                Code = dto.Code,
                Name = dto.Name,
                NameEn = dto.NameEn,
                Description = dto.Description,
                Type = dto.Type,
                Nature = dto.Nature,
                ParentId = dto.ParentId,
                TenantId = dto.TenantId,
                IsSystem = false
            };

            await _unitOfWork.ChartOfAccounts.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<AccountResponseDto>.Ok(
                MapAccount(account, new List<ChartOfAccount>(), new Dictionary<Guid, decimal>()),
                "Account created");
        }

        public async Task<ApiResponse<AccountResponseDto>> UpdateAccountAsync(Guid id, UpdateAccountDto dto)
        {
            var account = await _unitOfWork.ChartOfAccounts.GetByIdAsync(id);
            if (account == null) return ApiResponse<AccountResponseDto>.Fail("Account not found");

            account.Name = dto.Name;
            account.NameEn = dto.NameEn;
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.ChartOfAccounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync();

            var balances = await GetAccountBalancesAsync(account.TenantId ?? Guid.Empty);
            var all = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == account.TenantId);
            return ApiResponse<AccountResponseDto>.Ok(MapAccount(account, all.ToList(), balances), "Account updated");
        }

        public async Task<ApiResponse<bool>> ToggleAccountStatusAsync(Guid id)
        {
            var account = await _unitOfWork.ChartOfAccounts.GetByIdAsync(id);
            if (account == null) return ApiResponse<bool>.Fail("Account not found");
            if (account.IsSystem) return ApiResponse<bool>.Fail("Cannot deactivate a system account");

            account.IsActive = !account.IsActive;
            account.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.ChartOfAccounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, account.IsActive ? "Account activated" : "Account deactivated");
        }

        // ════════════════════════════════════════════════════════════════════
        // DEFAULT ACCOUNTS INITIALIZATION
        // ════════════════════════════════════════════════════════════════════

        public async Task InitializeDefaultAccountsAsync(Guid tenantId)
        {
            var existing = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == tenantId);
            if (existing.Any()) return;

            var defaults = new List<ChartOfAccount>
            {
                new() { Code = "1000", Name = "الأصول",           NameEn = "Assets",             Type = AccountType.Asset,     Nature = AccountNature.Debit,  IsSystem = true, TenantId = tenantId },
                new() { Code = CASH,   Name = "الصندوق والبنك",   NameEn = "Cash & Bank",         Type = AccountType.Asset,     Nature = AccountNature.Debit,  IsSystem = true, TenantId = tenantId },
                new() { Code = ACCOUNTS_RECEIVABLE, Name = "المدينون", NameEn = "Accounts Receivable", Type = AccountType.Asset, Nature = AccountNature.Debit, IsSystem = true, TenantId = tenantId },
                new() { Code = INVENTORY, Name = "المخزون",       NameEn = "Inventory",           Type = AccountType.Asset,     Nature = AccountNature.Debit,  IsSystem = true, TenantId = tenantId },
                new() { Code = "2000", Name = "الالتزامات",       NameEn = "Liabilities",         Type = AccountType.Liability, Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = ACCOUNTS_PAYABLE, Name = "الدائنون", NameEn = "Accounts Payable",  Type = AccountType.Liability, Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = "3000", Name = "حقوق الملكية",     NameEn = "Equity",              Type = AccountType.Equity,    Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = OWNER_EQUITY,     Name = "رأس المال",     NameEn = "Owner Equity",   Type = AccountType.Equity,    Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = RETAINED_EARNINGS, Name = "الأرباح المحتجزة", NameEn = "Retained Earnings", Type = AccountType.Equity, Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = "4000", Name = "الإيرادات",        NameEn = "Revenue",             Type = AccountType.Revenue,   Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = SALES_REVENUE,   Name = "إيرادات المبيعات", NameEn = "Sales Revenue", Type = AccountType.Revenue, Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = SUBSCRIPTION_REV, Name = "إيرادات الاشتراكات", NameEn = "Subscription Revenue", Type = AccountType.Revenue, Nature = AccountNature.Credit, IsSystem = true, TenantId = tenantId },
                new() { Code = REFUNDS_EXPENSE, Name = "مصاريف المرتجعات", NameEn = "Refund Expenses", Type = AccountType.Revenue, Nature = AccountNature.Debit, IsSystem = true, TenantId = tenantId },
                new() { Code = "5000", Name = "المصاريف",         NameEn = "Expenses",            Type = AccountType.Expense,   Nature = AccountNature.Debit,  IsSystem = true, TenantId = tenantId },
                new() { Code = COGS,   Name = "تكلفة المبيعات",  NameEn = "Cost of Goods Sold",  Type = AccountType.Expense,   Nature = AccountNature.Debit,  IsSystem = true, TenantId = tenantId },
                new() { Code = STOCK_LOSS, Name = "خسائر المخزون", NameEn = "Inventory Loss",    Type = AccountType.Expense,   Nature = AccountNature.Debit,  IsSystem = true, TenantId = tenantId },
            };

            var parentMap = new Dictionary<string, string>
            {
                [CASH] = "1000",
                [ACCOUNTS_RECEIVABLE] = "1000",
                [INVENTORY] = "1000",
                [ACCOUNTS_PAYABLE] = "2000",
                [OWNER_EQUITY] = "3000",
                [RETAINED_EARNINGS] = "3000",
                [SALES_REVENUE] = "4000",
                [SUBSCRIPTION_REV] = "4000",
                [REFUNDS_EXPENSE] = "4000",
                [COGS] = "5000",
                [STOCK_LOSS] = "5000",
            };

            foreach (var account in defaults)
                await _unitOfWork.ChartOfAccounts.AddAsync(account);

            await _unitOfWork.SaveChangesAsync();

            var saved = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == tenantId);
            var codeMap = saved.ToDictionary(a => a.Code);

            foreach (var (childCode, parentCode) in parentMap)
            {
                if (codeMap.TryGetValue(childCode, out var child) &&
                    codeMap.TryGetValue(parentCode, out var par))
                {
                    child.ParentId = par.Id;
                    child.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.ChartOfAccounts.UpdateAsync(child);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        // JOURNAL ENTRIES — CRUD
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<JournalEntryResponseDto>> CreateManualEntryAsync(CreateJournalEntryDto dto)
        {
            var validation = ValidateDoubleEntry(dto.Lines);
            if (!validation.IsValid)
                return ApiResponse<JournalEntryResponseDto>.Fail(validation.Error);

            var entry = new JournalEntry
            {
                EntryNumber = await GenerateEntryNumberAsync(),
                EntryDate = dto.EntryDate,
                Description = dto.Description,
                Notes = dto.Notes,
                Source = JournalEntrySource.Manual,
                Status = JournalEntryStatus.Draft,
                TotalDebit = dto.Lines.Sum(l => l.Debit),
                TotalCredit = dto.Lines.Sum(l => l.Credit),
                CreatedById = dto.CreatedById,
                TenantId = dto.TenantId
            };

            await _unitOfWork.JournalEntries.AddAsync(entry);

            foreach (var lineDto in dto.Lines)
            {
                var account = await _unitOfWork.ChartOfAccounts.GetByIdAsync(lineDto.AccountId);
                if (account == null)
                    return ApiResponse<JournalEntryResponseDto>.Fail($"Account {lineDto.AccountId} not found");

                await _unitOfWork.JournalEntryLines.AddAsync(new JournalEntryLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = lineDto.AccountId,
                    AccountCode = account.Code,
                    AccountName = account.Name,
                    Debit = lineDto.Debit,
                    Credit = lineDto.Credit,
                    Description = lineDto.Description
                });
            }

            await _unitOfWork.SaveChangesAsync();
            await LoadEntryNavigationsAsync(entry);
            return ApiResponse<JournalEntryResponseDto>.Ok(MapEntry(entry), "Journal entry created");
        }

        public async Task<ApiResponse<JournalEntryResponseDto>> GetEntryByIdAsync(Guid id)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdAsync(id);
            if (entry == null) return ApiResponse<JournalEntryResponseDto>.Fail("Entry not found");
            await LoadEntryNavigationsAsync(entry);
            return ApiResponse<JournalEntryResponseDto>.Ok(MapEntry(entry));
        }

        public async Task<ApiResponse<PagedResponse<JournalEntryResponseDto>>> GetEntriesByTenantAsync(
            Guid tenantId, PaginationParams pagination, DateTime? from = null, DateTime? to = null)
        {
            var (items, total) = await _unitOfWork.JournalEntries.GetPagedAsync(
                e => e.TenantId == tenantId &&
                     (!from.HasValue || e.EntryDate >= from.Value) &&
                     (!to.HasValue || e.EntryDate <= to.Value),
                pagination.Skip, pagination.PageSize);

            foreach (var e in items) await LoadEntryNavigationsAsync(e);

            return ApiResponse<PagedResponse<JournalEntryResponseDto>>.Ok(
                PagedResponse<JournalEntryResponseDto>.Create(
                    items.Select(MapEntry).ToList(), total, pagination));
        }

        public async Task<ApiResponse<bool>> PostEntryAsync(Guid id)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdAsync(id);
            if (entry == null) return ApiResponse<bool>.Fail("Entry not found");
            if (entry.Status != JournalEntryStatus.Draft)
                return ApiResponse<bool>.Fail("Only Draft entries can be posted");

            entry.Status = JournalEntryStatus.Posted;
            entry.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.JournalEntries.UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Entry posted");
        }

        public async Task<ApiResponse<bool>> ReverseEntryAsync(Guid id, Guid reversedById)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdAsync(id);
            if (entry == null) return ApiResponse<bool>.Fail("Entry not found");
            if (entry.Status != JournalEntryStatus.Posted)
                return ApiResponse<bool>.Fail("Only Posted entries can be reversed");

            await LoadEntryNavigationsAsync(entry);

            var reversal = new JournalEntry
            {
                EntryNumber = await GenerateEntryNumberAsync(),
                EntryDate = DateTime.UtcNow,
                Description = $"عكس القيد: {entry.EntryNumber} — {entry.Description}",
                Source = entry.Source,
                Status = JournalEntryStatus.Posted,
                ReferenceId = entry.ReferenceId,
                ReferenceNumber = entry.ReferenceNumber,
                TotalDebit = entry.TotalCredit,
                TotalCredit = entry.TotalDebit,
                CreatedById = reversedById,
                TenantId = entry.TenantId
            };

            await _unitOfWork.JournalEntries.AddAsync(reversal);

            foreach (var line in entry.Lines)
            {
                await _unitOfWork.JournalEntryLines.AddAsync(new JournalEntryLine
                {
                    JournalEntryId = reversal.Id,
                    AccountId = line.AccountId,
                    AccountCode = line.AccountCode,
                    AccountName = line.AccountName,
                    Debit = line.Credit,
                    Credit = line.Debit,
                    Description = $"عكس: {line.Description}"
                });
            }

            entry.Status = JournalEntryStatus.Reversed;
            entry.ReversedByEntryId = reversal.Id;
            entry.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.JournalEntries.UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Entry reversed");
        }

        // ════════════════════════════════════════════════════════════════════
        // AUTO-JOURNAL TRIGGERS
        // ════════════════════════════════════════════════════════════════════

        public async Task CreateInvoicePaidEntryAsync(Guid invoiceId, Guid tenantId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null) return;

            await CreateAutoEntryAsync(tenantId,
                description: $"فاتورة مدفوعة #{invoice.InvoiceNumber}",
                source: JournalEntrySource.Invoice,
                referenceId: invoiceId,
                referenceNumber: invoice.InvoiceNumber,
                debitCode: CASH,
                creditCode: SALES_REVENUE,
                amount: invoice.Total);
        }

        public async Task CreateOrderPaidEntryAsync(Guid orderId, Guid tenantId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) return;

            await CreateAutoEntryAsync(tenantId,
                description: $"طلب مدفوع #{order.OrderNumber}",
                source: JournalEntrySource.Order,
                referenceId: orderId,
                referenceNumber: order.OrderNumber,
                debitCode: CASH,
                creditCode: SALES_REVENUE,
                amount: order.Total);
        }

        public async Task CreateRefundEntryAsync(Guid returnRequestId, Guid tenantId)
        {
            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == tenantId);
            var accountMap = accounts.ToDictionary(a => a.Code);

            if (!accountMap.TryGetValue(REFUNDS_EXPENSE, out var debitAcc) ||
                !accountMap.TryGetValue(CASH, out var creditAcc)) return;

            var returnItems = await _unitOfWork.ReturnRequests.GetByIdAsync(returnRequestId);
            if (returnItems == null) return;

            await CreateAutoEntryAsync(tenantId,
                description: $"مرتجع #{returnItems.ReturnNumber}",
                source: JournalEntrySource.Refund,
                referenceId: returnRequestId,
                referenceNumber: returnItems.ReturnNumber,
                debitCode: REFUNDS_EXPENSE,
                creditCode: CASH,
                amount: returnItems.ApprovedAmount);
        }

        public async Task CreateStockMovementEntryAsync(Guid stockMovementId, Guid tenantId)
        {
            var movement = await _unitOfWork.StockMovements.GetByIdAsync(stockMovementId);
            if (movement == null || !movement.UnitCost.HasValue) return;

            decimal amount = Math.Abs(movement.Quantity) * movement.UnitCost.Value;
            if (amount <= 0) return;

            string debitCode, creditCode, description;

            switch (movement.Type)
            {
                case StockMovementType.Purchase:
                    debitCode = INVENTORY; creditCode = ACCOUNTS_PAYABLE;
                    description = $"استلام مخزون — {movement.Reference}";
                    break;
                case StockMovementType.Sale:
                    debitCode = COGS; creditCode = INVENTORY;
                    description = $"تكلفة مبيعات — {movement.Reference}";
                    break;
                case StockMovementType.Damage:
                    debitCode = STOCK_LOSS; creditCode = INVENTORY;
                    description = $"خسارة مخزون — {movement.Reference}";
                    break;
                default:
                    return;
            }

            await CreateAutoEntryAsync(tenantId, description,
                JournalEntrySource.StockMovement,
                stockMovementId, movement.Reference,
                debitCode, creditCode, amount);
        }

        public async Task CreateSubscriptionPaidEntryAsync(Guid subscriptionId, Guid tenantId)
        {
            var sub = await _unitOfWork.Subscriptions.GetByIdAsync(subscriptionId);
            if (sub == null) return;

            await CreateAutoEntryAsync(tenantId,
                description: $"اشتراك #{subscriptionId} — {sub.Period}",
                source: JournalEntrySource.Subscription,
                referenceId: subscriptionId,
                referenceNumber: subscriptionId.ToString()[..8].ToUpper(),
                debitCode: CASH,
                creditCode: SUBSCRIPTION_REV,
                amount: sub.Price);
        }

        // ════════════════════════════════════════════════════════════════════
        // REPORTS
        // ════════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<TrialBalanceDto>> GetTrialBalanceAsync(ReportFilterDto filter)
        {
            var entries = await GetPostedEntriesInRangeAsync(filter);
            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == filter.TenantId);

            var lines = accounts
                .Where(a => a.ParentId != null)
                .Select(account =>
                {
                    var accountLines = entries.SelectMany(e => e.Lines)
                        .Where(l => l.AccountId == account.Id).ToList();

                    return new TrialBalanceLineDto
                    {
                        AccountCode = account.Code,
                        AccountName = account.Name,
                        AccountType = account.Type,
                        TotalDebit = accountLines.Sum(l => l.Debit),
                        TotalCredit = accountLines.Sum(l => l.Credit),
                        Balance = accountLines.Sum(l => l.Debit) - accountLines.Sum(l => l.Credit)
                    };
                })
                .Where(l => l.TotalDebit != 0 || l.TotalCredit != 0)
                .OrderBy(l => l.AccountCode)
                .ToList();

            return ApiResponse<TrialBalanceDto>.Ok(new TrialBalanceDto
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                Lines = lines,
                TotalDebit = lines.Sum(l => l.TotalDebit),
                TotalCredit = lines.Sum(l => l.TotalCredit)
            });
        }

        public async Task<ApiResponse<ProfitAndLossDto>> GetProfitAndLossAsync(ReportFilterDto filter)
        {
            var entries = await GetPostedEntriesInRangeAsync(filter);
            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == filter.TenantId);

            List<PLSectionDto> GetSection(AccountType type) =>
                accounts.Where(a => a.Type == type && a.ParentId != null)
                    .Select(acc => new PLSectionDto
                    {
                        AccountCode = acc.Code,
                        AccountName = acc.Name,
                        Amount = entries.SelectMany(e => e.Lines)
                            .Where(l => l.AccountId == acc.Id)
                            .Sum(l => type == AccountType.Revenue ? l.Credit - l.Debit : l.Debit - l.Credit)
                    })
                    .Where(s => s.Amount != 0)
                    .OrderBy(s => s.AccountCode)
                    .ToList();

            var revenue = GetSection(AccountType.Revenue);
            var expenses = GetSection(AccountType.Expense);
            decimal totalRev = revenue.Sum(r => r.Amount);
            decimal totalExp = expenses.Sum(e => e.Amount);
            decimal netProfit = totalRev - totalExp;

            return ApiResponse<ProfitAndLossDto>.Ok(new ProfitAndLossDto
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                Revenue = revenue,
                Expenses = expenses,
                TotalRevenue = totalRev,
                TotalExpenses = totalExp,
                GrossProfit = totalRev,
                NetProfit = netProfit,
                NetProfitMargin = totalRev > 0 ? Math.Round(netProfit / totalRev * 100, 2) : 0
            });
        }

        public async Task<ApiResponse<BalanceSheetDto>> GetBalanceSheetAsync(ReportFilterDto filter)
        {
            var allEntries = await _unitOfWork.JournalEntries.FindAsync(
                e => e.TenantId == filter.TenantId &&
                     e.Status == JournalEntryStatus.Posted &&
                     e.EntryDate <= filter.ToDate);

            foreach (var e in allEntries)
                e.Lines = (await _unitOfWork.JournalEntryLines.FindAsync(l => l.JournalEntryId == e.Id)).ToList();

            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(a => a.TenantId == filter.TenantId);

            List<BSLineDto> GetSection(AccountType type) =>
                accounts.Where(a => a.Type == type && a.ParentId != null)
                    .Select(acc => new BSLineDto
                    {
                        AccountCode = acc.Code,
                        AccountName = acc.Name,
                        Balance = allEntries.SelectMany(e => e.Lines)
                            .Where(l => l.AccountId == acc.Id)
                            .Sum(l => acc.Nature == AccountNature.Debit
                                ? l.Debit - l.Credit
                                : l.Credit - l.Debit)
                    })
                    .Where(s => s.Balance != 0)
                    .OrderBy(s => s.AccountCode)
                    .ToList();

            var assets = GetSection(AccountType.Asset);
            var liabilities = GetSection(AccountType.Liability);
            var equity = GetSection(AccountType.Equity);

            return ApiResponse<BalanceSheetDto>.Ok(new BalanceSheetDto
            {
                AsOfDate = filter.ToDate,
                Assets = assets,
                Liabilities = liabilities,
                Equity = equity,
                TotalAssets = assets.Sum(a => a.Balance),
                TotalLiabilities = liabilities.Sum(l => l.Balance),
                TotalEquity = equity.Sum(e => e.Balance)
            });
        }

        public async Task<ApiResponse<CashFlowDto>> GetCashFlowAsync(ReportFilterDto filter)
        {
            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(
                a => a.TenantId == filter.TenantId && a.Code == CASH);
            var cashAccount = accounts.FirstOrDefault();
            if (cashAccount == null)
                return ApiResponse<CashFlowDto>.Fail("Cash account not found");

            var allLines = await _unitOfWork.JournalEntryLines.FindAsync(l => l.AccountId == cashAccount.Id);
            var entries = await _unitOfWork.JournalEntries.FindAsync(
                e => e.TenantId == filter.TenantId && e.Status == JournalEntryStatus.Posted);
            var entryMap = entries.ToDictionary(e => e.Id);

            var inRange = allLines.Where(l => entryMap.TryGetValue(l.JournalEntryId, out var e) &&
                                              e.EntryDate >= filter.FromDate &&
                                              e.EntryDate <= filter.ToDate).ToList();

            var beforeRange = allLines.Where(l => entryMap.TryGetValue(l.JournalEntryId, out var e) &&
                                                  e.EntryDate < filter.FromDate).ToList();
            decimal openingBalance = beforeRange.Sum(l => l.Debit - l.Credit);

            var operatingLines = new List<CashFlowLineDto>();
            var investingLines = new List<CashFlowLineDto>();
            var financingLines = new List<CashFlowLineDto>();

            foreach (var line in inRange)
            {
                if (!entryMap.TryGetValue(line.JournalEntryId, out var entry)) continue;
                var flowLine = new CashFlowLineDto { Description = entry.Description, Amount = line.Debit - line.Credit };

                switch (entry.Source)
                {
                    case JournalEntrySource.Invoice:
                    case JournalEntrySource.Order:
                    case JournalEntrySource.Refund:
                        operatingLines.Add(flowLine); break;
                    case JournalEntrySource.StockMovement:
                        investingLines.Add(flowLine); break;
                    case JournalEntrySource.Subscription:
                        financingLines.Add(flowLine); break;
                    default:
                        operatingLines.Add(flowLine); break;
                }
            }

            decimal operating = operatingLines.Sum(l => l.Amount);
            decimal investing = investingLines.Sum(l => l.Amount);
            decimal financing = financingLines.Sum(l => l.Amount);
            decimal netCash = operating + investing + financing;

            return ApiResponse<CashFlowDto>.Ok(new CashFlowDto
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                OperatingCashFlow = operating,
                InvestingCashFlow = investing,
                FinancingCashFlow = financing,
                NetCashFlow = netCash,
                OpeningBalance = openingBalance,
                ClosingBalance = openingBalance + netCash,
                OperatingLines = operatingLines,
                InvestingLines = investingLines,
                FinancingLines = financingLines
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        private async Task CreateAutoEntryAsync(
            Guid tenantId, string description,
            JournalEntrySource source, Guid referenceId, string referenceNumber,
            string debitCode, string creditCode, decimal amount)
        {
            if (amount <= 0) return;

            var accounts = await _unitOfWork.ChartOfAccounts.FindAsync(
                a => a.TenantId == tenantId && (a.Code == debitCode || a.Code == creditCode));

            var debitAcc = accounts.FirstOrDefault(a => a.Code == debitCode);
            var creditAcc = accounts.FirstOrDefault(a => a.Code == creditCode);
            if (debitAcc == null || creditAcc == null) return;

            var entry = new JournalEntry
            {
                EntryNumber = await GenerateEntryNumberAsync(),
                EntryDate = DateTime.UtcNow,
                Description = description,
                Source = source,
                Status = JournalEntryStatus.Posted,
                ReferenceId = referenceId,
                ReferenceNumber = referenceNumber,
                TotalDebit = amount,
                TotalCredit = amount,
                TenantId = tenantId
            };

            await _unitOfWork.JournalEntries.AddAsync(entry);

            await _unitOfWork.JournalEntryLines.AddAsync(new JournalEntryLine
            {
                JournalEntryId = entry.Id,
                AccountId = debitAcc.Id,
                AccountCode = debitAcc.Code,
                AccountName = debitAcc.Name,
                Debit = amount,
                Credit = 0,
                Description = description
            });

            await _unitOfWork.JournalEntryLines.AddAsync(new JournalEntryLine
            {
                JournalEntryId = entry.Id,
                AccountId = creditAcc.Id,
                AccountCode = creditAcc.Code,
                AccountName = creditAcc.Name,
                Debit = 0,
                Credit = amount,
                Description = description
            });

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<List<JournalEntry>> GetPostedEntriesInRangeAsync(ReportFilterDto filter)
        {
            var entries = await _unitOfWork.JournalEntries.FindAsync(
                e => e.TenantId == filter.TenantId &&
                     e.Status == JournalEntryStatus.Posted &&
                     e.EntryDate >= filter.FromDate &&
                     e.EntryDate <= filter.ToDate);

            foreach (var e in entries)
                e.Lines = (await _unitOfWork.JournalEntryLines.FindAsync(l => l.JournalEntryId == e.Id)).ToList();

            return entries.ToList();
        }

        private async Task<Dictionary<Guid, decimal>> GetAccountBalancesAsync(Guid tenantId)
        {
            var entries = await _unitOfWork.JournalEntries.FindAsync(
                e => e.TenantId == tenantId && e.Status == JournalEntryStatus.Posted);
            var entryIds = entries.Select(e => e.Id).ToHashSet();

            if (!entryIds.Any())
                return new Dictionary<Guid, decimal>();

            var lines = await _unitOfWork.JournalEntryLines.FindAsync(
                l => entryIds.Contains(l.JournalEntryId));

            return lines
                .GroupBy(l => l.AccountId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));
        }

        private static (bool IsValid, string Error) ValidateDoubleEntry(List<CreateJournalEntryLineDto> lines)
        {
            if (lines.Count < 2)
                return (false, "Journal entry must have at least 2 lines");
            if (lines.Any(l => l.Debit < 0 || l.Credit < 0))
                return (false, "Debit and Credit amounts cannot be negative");
            if (lines.Any(l => l.Debit > 0 && l.Credit > 0))
                return (false, "A single line cannot have both Debit and Credit amounts");

            decimal totalDebit = lines.Sum(l => l.Debit);
            decimal totalCredit = lines.Sum(l => l.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.001m)
                return (false, $"Total Debit ({totalDebit:N2}) must equal Total Credit ({totalCredit:N2})");

            return (true, string.Empty);
        }

        private async Task<string> GenerateEntryNumberAsync()
        {
            string number;
            bool exists;
            do
            {
                number = $"JE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
                var found = await _unitOfWork.JournalEntries.FindAsync(e => e.EntryNumber == number);
                exists = found.Any();
            } while (exists);
            return number;
        }

        private async Task LoadEntryNavigationsAsync(JournalEntry entry)
        {
            entry.Lines = (await _unitOfWork.JournalEntryLines
                .FindAsync(l => l.JournalEntryId == entry.Id))
                .OrderBy(l => l.AccountCode)
                .ToList();

            if (entry.CreatedById.HasValue)
                entry.CreatedBy = await _unitOfWork.Users.GetByIdAsync(entry.CreatedById.Value);
        }

        private AccountResponseDto MapAccount(
            ChartOfAccount account,
            List<ChartOfAccount> all,
            Dictionary<Guid, decimal> balances)
        {
            balances.TryGetValue(account.Id, out decimal balance);
            var children = all.Where(a => a.ParentId == account.Id)
                              .Select(c => MapAccount(c, all, balances))
                              .ToList();

            return new AccountResponseDto
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                NameEn = account.NameEn,
                Description = account.Description,
                Type = account.Type,
                TypeName = account.Type.ToString(),
                Nature = account.Nature,
                NatureName = account.Nature.ToString(),
                IsActive = account.IsActive,
                IsSystem = account.IsSystem,
                ParentId = account.ParentId,
                Balance = balance,
                Children = children
            };
        }

        private static JournalEntryResponseDto MapEntry(JournalEntry e) => new()
        {
            Id = e.Id,
            EntryNumber = e.EntryNumber,
            EntryDate = e.EntryDate,
            Description = e.Description,
            Source = e.Source,
            SourceName = e.Source.ToString(),
            Status = e.Status,
            StatusName = e.Status.ToString(),
            ReferenceId = e.ReferenceId,
            ReferenceNumber = e.ReferenceNumber,
            TotalDebit = e.TotalDebit,
            TotalCredit = e.TotalCredit,
            Notes = e.Notes,
            CreatedByName = e.CreatedBy != null
                              ? $"{e.CreatedBy.FirstName} {e.CreatedBy.LastName}".Trim()
                              : string.Empty,
            TenantId = e.TenantId,
            CreatedAt = e.CreatedAt,
            Lines = e.Lines.Select(l => new JournalEntryLineDto
            {
                Id = l.Id,
                AccountId = l.AccountId,
                AccountCode = l.AccountCode,
                AccountName = l.AccountName,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            }).ToList()
        };
    }
}