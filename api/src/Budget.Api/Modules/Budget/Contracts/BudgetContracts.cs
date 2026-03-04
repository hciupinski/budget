using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Budget.Contracts;

public sealed record BudgetCategoryResponse(
    Guid Id,
    string Name,
    string Section,
    int SortOrder);

public sealed record AnnualPlanResponse(
    int Year,
    IReadOnlyList<AnnualCategoryRowResponse> Categories,
    AnnualSummaryResponse Summary);

public sealed record AnnualCategoryRowResponse(
    Guid CategoryId,
    string CategoryName,
    string Section,
    int SortOrder,
    IReadOnlyList<decimal> Months,
    decimal Total);

public sealed record AnnualSummaryResponse(
    decimal Income,
    decimal Costs,
    decimal SavingsInvestments,
    decimal Remainder,
    decimal GrandTotal);

public sealed record UpdateAnnualPlanRequest(
    [property: Required] IReadOnlyList<AnnualCellInput> Cells);

public sealed record AnnualCellInput(
    Guid CategoryId,
    [property: Range(1, 12)] int Month,
    decimal PlannedAmount);

public sealed record MonthlyWorkspaceResponse(
    int Year,
    int Month,
    string StatusFilter,
    MonthlySummaryCardsResponse Summary,
    MonthlyCompletionResponse Completion,
    IReadOnlyList<MonthlyActionResponse> Actions);

public sealed record MonthlySummaryCardsResponse(
    decimal IncomePlanned,
    decimal IncomeActual,
    decimal CostsPlanned,
    decimal CostsActual,
    decimal SavingsPlanned,
    decimal SavingsActual,
    decimal RemainderPlanned,
    decimal RemainderActual);

public sealed record MonthlyCompletionResponse(
    int Planned,
    int Done,
    int Partial,
    int Skipped,
    int Total);

public sealed record MonthlyActionResponse(
    Guid ActionId,
    Guid CategoryId,
    string CategoryName,
    string Section,
    decimal PlannedAmount,
    decimal? ActualAmount,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record GenerateMonthlyActionsResponse(
    int Created,
    int Updated,
    int Total);

public sealed record UpdateMonthlyActionRequest(
    string? Status,
    decimal? ActualAmount);

public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset ChangedAt,
    string EntityType,
    Guid EntityId,
    string EventType,
    string ChangedBy,
    string Payload);

public sealed record ManagedSectionResponse(
    string Id,
    string Name,
    string Kind,
    IReadOnlyList<string> Keywords,
    int Order);

public sealed record ManagedSectionsResponse(
    IReadOnlyList<ManagedSectionResponse> Sections);

public sealed record ManagedSectionInput(
    [property: Required] string Id,
    [property: Required] string Name,
    [property: Required] string Kind,
    IReadOnlyList<string>? Keywords,
    int Order);

public sealed record UpdateManagedSectionsRequest(
    [property: Required] IReadOnlyList<ManagedSectionInput> Sections);

public sealed record AnnualCustomItemResponse(
    string Id,
    int Year,
    string SectionId,
    string SectionKind,
    string Name,
    IReadOnlyList<decimal> Months);

public sealed record MonthlyCustomItemResponse(
    string Id,
    int Year,
    int Month,
    string SectionId,
    string SectionKind,
    string Name,
    decimal PlannedAmount,
    decimal? ActualAmount,
    string Status,
    string? AnnualCustomItemId);

public sealed record PlannerCustomizationResponse(
    IReadOnlyList<AnnualCustomItemResponse> AnnualCustomItems,
    IReadOnlyList<MonthlyCustomItemResponse> MonthlyCustomItems,
    IReadOnlyDictionary<string, string> NameOverrides,
    IReadOnlyList<string> HiddenAnnualApiRows,
    IReadOnlyList<string> HiddenMonthlyApiRows,
    IReadOnlyList<string> OneTimeAnnualRows);

public sealed record AnnualCustomItemInput(
    [property: Required] string Id,
    int Year,
    [property: Required] string SectionId,
    [property: Required] string SectionKind,
    [property: Required] string Name,
    IReadOnlyList<decimal>? Months);

public sealed record MonthlyCustomItemInput(
    [property: Required] string Id,
    int Year,
    [property: Range(1, 12)] int Month,
    [property: Required] string SectionId,
    [property: Required] string SectionKind,
    [property: Required] string Name,
    decimal PlannedAmount,
    decimal? ActualAmount,
    [property: Required] string Status,
    string? AnnualCustomItemId);

public sealed record UpdatePlannerCustomizationRequest(
    IReadOnlyList<AnnualCustomItemInput>? AnnualCustomItems,
    IReadOnlyList<MonthlyCustomItemInput>? MonthlyCustomItems,
    IReadOnlyDictionary<string, string>? NameOverrides,
    IReadOnlyList<string>? HiddenAnnualApiRows,
    IReadOnlyList<string>? HiddenMonthlyApiRows,
    IReadOnlyList<string>? OneTimeAnnualRows);

public sealed record GeneralSettingsResponse(
    string Currency,
    string Theme);

public sealed record UpdateGeneralSettingsRequest(
    string? Currency,
    string? Theme);

public sealed record BudgetAccountResponse(
    Guid Id,
    string Name,
    string Kind,
    string Currency,
    decimal CurrentBalance,
    bool IsArchived,
    DateTimeOffset UpdatedAt);

public sealed record CreateBudgetAccountRequest(
    [property: Required] string Name,
    [property: Required] string Kind,
    [property: Required] string Currency,
    decimal InitialBalance);

public sealed record UpdateBudgetAccountRequest(
    string? Name,
    string? Kind,
    string? Currency,
    decimal? CurrentBalance,
    bool? IsArchived);

public sealed record AccountTransferResponse(
    Guid Id,
    Guid FromAccountId,
    string FromAccountName,
    Guid ToAccountId,
    string ToAccountName,
    decimal Amount,
    string Note,
    DateTimeOffset TransferDate);

public sealed record CreateAccountTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    [property: Range(typeof(decimal), "0.01", "999999999")] decimal Amount,
    string? Note,
    DateTimeOffset? TransferDate);

public sealed record AccountSnapshotResponse(
    Guid AccountId,
    string AccountName,
    string AccountKind,
    int Year,
    int Month,
    decimal PlannedBalance,
    decimal? ActualBalance,
    DateTimeOffset UpdatedAt);

public sealed record AccountSnapshotInput(
    Guid AccountId,
    decimal PlannedBalance,
    decimal? ActualBalance);

public sealed record UpsertMonthlyAccountSnapshotsRequest(
    [property: Required] IReadOnlyList<AccountSnapshotInput> Snapshots);

public sealed record InvestmentHoldingResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string AccountCurrency,
    string Symbol,
    decimal Units,
    decimal AverageCost,
    decimal? ManualPriceOverride,
    decimal LastFetchedPrice,
    decimal EffectivePrice,
    decimal CurrentValue,
    decimal CostBasis,
    decimal ProfitLoss,
    DateTimeOffset LastPriceUpdatedAt);

public sealed record CreateInvestmentHoldingRequest(
    Guid AccountId,
    [property: Required] string Symbol,
    decimal Units,
    decimal AverageCost,
    decimal? ManualPriceOverride);

public sealed record UpdateInvestmentHoldingRequest(
    decimal? Units,
    decimal? AverageCost,
    decimal? ManualPriceOverride,
    bool ClearManualPriceOverride);

public sealed record RefreshInvestmentPricesResponse(
    int UpdatedCount,
    DateTimeOffset RefreshedAt,
    IReadOnlyList<string> Symbols);

public sealed record InvestmentAllocationResponse(
    Guid HoldingId,
    string Symbol,
    decimal CurrentValue,
    decimal AllocationPercent);

public sealed record InvestmentsDashboardResponse(
    decimal TotalValue,
    decimal TotalCostBasis,
    decimal TotalProfitLoss,
    IReadOnlyList<InvestmentAllocationResponse> Allocation);

public sealed record SavingsGoalResponse(
    Guid Id,
    string Name,
    Guid? AccountId,
    string? AccountName,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal MonthlyContributionTarget,
    int? TargetYear,
    int? TargetMonth,
    decimal ProgressPercent,
    DateTimeOffset UpdatedAt);

public sealed record CreateSavingsGoalRequest(
    [property: Required] string Name,
    Guid? AccountId,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal MonthlyContributionTarget,
    int? TargetYear,
    int? TargetMonth);

public sealed record UpdateSavingsGoalRequest(
    string? Name,
    Guid? AccountId,
    decimal? TargetAmount,
    decimal? CurrentAmount,
    decimal? MonthlyContributionTarget,
    int? TargetYear,
    int? TargetMonth,
    bool ClearTargetDate);

public sealed record AssetsOverviewResponse(
    int Year,
    int Month,
    AssetsOverviewSummaryResponse Summary,
    IReadOnlyList<BudgetAccountResponse> Accounts,
    IReadOnlyList<AccountTransferResponse> Transfers,
    IReadOnlyList<AccountSnapshotResponse> Snapshots,
    IReadOnlyList<InvestmentHoldingResponse> Holdings,
    InvestmentsDashboardResponse Investments,
    IReadOnlyList<SavingsGoalResponse> SavingsGoals);

public sealed record AssetsOverviewSummaryResponse(
    string BaseCurrency,
    decimal NetWorth,
    decimal SnapshotPlanned,
    decimal SnapshotActual,
    IReadOnlyDictionary<string, decimal> ExchangeRates);

public sealed record AssetsAccountsOverviewResponse(
    int Year,
    int Month,
    AssetsOverviewSummaryResponse Summary,
    IReadOnlyList<BudgetAccountResponse> Accounts,
    IReadOnlyList<AccountTransferResponse> Transfers,
    IReadOnlyList<AccountSnapshotResponse> Snapshots,
    IReadOnlyList<SavingsGoalResponse> SavingsGoals);

public sealed record InvestmentPriceRefreshMetaResponse(
    DateTimeOffset RefreshedAtUtc,
    int CacheTtlMinutes,
    IReadOnlyList<string> SymbolsRequested,
    IReadOnlyList<string> SymbolsRefreshed,
    IReadOnlyList<string> SymbolsFromCache,
    IReadOnlyList<string> SymbolsFallbackToStale,
    bool HadProviderFailures);

public sealed record AssetsInvestmentsResponse(
    IReadOnlyList<InvestmentHoldingResponse> Holdings,
    InvestmentsDashboardResponse Investments,
    InvestmentPriceRefreshMetaResponse PriceRefreshMeta);
