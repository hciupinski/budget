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
