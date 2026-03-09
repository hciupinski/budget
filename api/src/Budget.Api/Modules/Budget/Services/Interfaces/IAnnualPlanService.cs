using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Modules.Budget.Services;

public interface IAnnualPlanService
{
    Task<AnnualPlanResponse> GetAnnualPlanAsync(int year, CancellationToken cancellationToken);
    Task UpsertAnnualPlanAsync(
        int year,
        UpdateAnnualPlanRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task CopyAnnualPlanAsync(int targetYear, int sourceYear, string actor, CancellationToken cancellationToken);
}
