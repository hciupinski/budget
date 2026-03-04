using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Modules.Budget.Services;

public interface IAccountsService
{
    Task<AssetsOverviewResponse> GetAssetsOverviewAsync(int year, int month, CancellationToken cancellationToken);
    Task<AssetsAccountsOverviewResponse> GetAccountsOverviewAsync(int year, int month, CancellationToken cancellationToken);
    Task<BudgetAccountResponse> CreateAccountAsync(
        CreateBudgetAccountRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<BudgetAccountResponse> UpdateAccountAsync(
        Guid accountId,
        UpdateBudgetAccountRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<AccountTransferResponse> CreateTransferAsync(
        CreateAccountTransferRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountSnapshotResponse>> UpsertAccountSnapshotsAsync(
        int year,
        int month,
        UpsertMonthlyAccountSnapshotsRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<SavingsGoalResponse> CreateSavingsGoalAsync(
        CreateSavingsGoalRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<SavingsGoalResponse> UpdateSavingsGoalAsync(
        Guid goalId,
        UpdateSavingsGoalRequest request,
        string actor,
        CancellationToken cancellationToken);
}
