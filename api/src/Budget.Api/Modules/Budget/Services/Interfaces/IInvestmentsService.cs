using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Modules.Budget.Services;

public interface IInvestmentsService
{
    Task<AssetsInvestmentsResponse> GetInvestmentsAsync(
        string actor,
        string? refreshMode,
        CancellationToken cancellationToken);
    Task<InvestmentHoldingResponse> CreateHoldingAsync(
        CreateInvestmentHoldingRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<InvestmentHoldingResponse> UpdateHoldingAsync(
        Guid holdingId,
        UpdateInvestmentHoldingRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task DeleteHoldingAsync(
        Guid holdingId,
        string actor,
        CancellationToken cancellationToken);
    Task<RefreshInvestmentPricesResponse> RefreshInvestmentPricesAsync(
        string actor,
        CancellationToken cancellationToken);
}
