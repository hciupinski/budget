using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Modules.Budget.Services;

public interface IMonthlyWorkspaceService
{
    Task<GenerateMonthlyActionsResponse> GenerateMonthlyActionsAsync(
        int year,
        int month,
        string actor,
        CancellationToken cancellationToken);
    Task<MonthlyWorkspaceResponse> GetMonthlyWorkspaceAsync(
        int year,
        int month,
        string? statusFilter,
        CancellationToken cancellationToken);
    Task<MonthlyActionResponse> UpdateMonthlyActionAsync(
        int year,
        int month,
        Guid actionId,
        UpdateMonthlyActionRequest request,
        string actor,
        CancellationToken cancellationToken);
}
