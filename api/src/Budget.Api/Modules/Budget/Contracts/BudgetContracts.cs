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
    string Status);

public sealed record PlannerCustomizationResponse(
    IReadOnlyList<AnnualCustomItemResponse> AnnualCustomItems,
    IReadOnlyList<MonthlyCustomItemResponse> MonthlyCustomItems,
    IReadOnlyDictionary<string, string> NameOverrides,
    IReadOnlyList<string> HiddenAnnualApiRows,
    IReadOnlyList<string> HiddenMonthlyApiRows);

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
    [property: Required] string Status);

public sealed record UpdatePlannerCustomizationRequest(
    IReadOnlyList<AnnualCustomItemInput>? AnnualCustomItems,
    IReadOnlyList<MonthlyCustomItemInput>? MonthlyCustomItems,
    IReadOnlyDictionary<string, string>? NameOverrides,
    IReadOnlyList<string>? HiddenAnnualApiRows,
    IReadOnlyList<string>? HiddenMonthlyApiRows);

public sealed record GeneralSettingsResponse(
    string Currency,
    string Theme);

public sealed record UpdateGeneralSettingsRequest(
    string? Currency,
    string? Theme);
