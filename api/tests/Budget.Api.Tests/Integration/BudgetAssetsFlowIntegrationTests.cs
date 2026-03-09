using System.Net.Http.Json;
using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Tests.Integration;

public sealed class BudgetAssetsFlowIntegrationTests
{
    [Fact]
    public async Task AssetsEndpoints_EndToEndFlow_Works()
    {
        await using var host = await BudgetApiIntegrationTestHost.StartAsync();
        var client = await host.CreateAuthenticatedClientAsync();

        var bankAccountResponse = await client.PostAsJsonAsync(
            "/api/budget/assets/accounts",
            new CreateBudgetAccountRequest(
                Name: "Main Bank",
                Kind: "BANK",
                Currency: "PLN",
                InitialBalance: 5000m));
        bankAccountResponse.EnsureSuccessStatusCode();
        var bank = await bankAccountResponse.Content.ReadFromJsonAsync<BudgetAccountResponse>();
        Assert.NotNull(bank);

        var brokerageAccountResponse = await client.PostAsJsonAsync(
            "/api/budget/assets/accounts",
            new CreateBudgetAccountRequest(
                Name: "Brokerage Account",
                Kind: "BROKERAGE",
                Currency: "PLN",
                InitialBalance: 1000m));
        brokerageAccountResponse.EnsureSuccessStatusCode();
        var brokerage = await brokerageAccountResponse.Content.ReadFromJsonAsync<BudgetAccountResponse>();
        Assert.NotNull(brokerage);

        var transferResponse = await client.PostAsJsonAsync(
            "/api/budget/assets/transfers",
            new CreateAccountTransferRequest(
                FromAccountId: bank.Id,
                ToAccountId: brokerage.Id,
                Amount: 300m,
                Note: "Monthly allocation",
                TransferDate: null));
        transferResponse.EnsureSuccessStatusCode();

        const int year = 2026;
        const int month = 4;

        var snapshotsResponse = await client.PutAsJsonAsync(
            $"/api/budget/assets/snapshots/{year}/{month}",
            new UpsertMonthlyAccountSnapshotsRequest(
                [
                    new AccountSnapshotInput(bank.Id, PlannedBalance: 4700m, ActualBalance: 4700m),
                    new AccountSnapshotInput(brokerage.Id, PlannedBalance: 1300m, ActualBalance: 1300m)
                ]));
        snapshotsResponse.EnsureSuccessStatusCode();
        var snapshots = await snapshotsResponse.Content.ReadFromJsonAsync<IReadOnlyList<AccountSnapshotResponse>>();
        Assert.NotNull(snapshots);
        Assert.Equal(2, snapshots.Count);

        var savingsGoalResponse = await client.PostAsJsonAsync(
            "/api/budget/assets/savings-goals",
            new CreateSavingsGoalRequest(
                Name: "Emergency Fund",
                AccountId: bank.Id,
                TargetAmount: 20000m,
                CurrentAmount: 4700m,
                MonthlyContributionTarget: 1000m,
                TargetYear: 2027,
                TargetMonth: 12));
        savingsGoalResponse.EnsureSuccessStatusCode();
        var goal = await savingsGoalResponse.Content.ReadFromJsonAsync<SavingsGoalResponse>();
        Assert.NotNull(goal);

        var overview = await client.GetFromJsonAsync<AssetsOverviewResponse>(
            $"/api/budget/assets/overview?year={year}&month={month}");
        Assert.NotNull(overview);
        Assert.Equal(2, overview.Accounts.Count);
        Assert.Single(overview.Transfers);
        Assert.Equal(2, overview.Snapshots.Count);
        Assert.Single(overview.SavingsGoals);

        var accountsOverview = await client.GetFromJsonAsync<AssetsAccountsOverviewResponse>(
            $"/api/budget/assets/accounts-overview?year={year}&month={month}");
        Assert.NotNull(accountsOverview);
        Assert.Equal(2, accountsOverview.Accounts.Count);
        Assert.Equal(2, accountsOverview.Snapshots.Count);
        Assert.Single(accountsOverview.SavingsGoals);

        var investments = await client.GetFromJsonAsync<AssetsInvestmentsResponse>(
            "/api/budget/assets/investments?refreshMode=none");
        Assert.NotNull(investments);
        Assert.Empty(investments.Holdings);
        Assert.Empty(investments.PriceRefreshMeta.SymbolsRequested);
    }
}
