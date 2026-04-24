using System.Net.Http.Json;
using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Tests.Integration;

public sealed class BudgetMonthlyFlowIntegrationTests
{
    [Fact]
    public async Task MonthlyEndpoints_EndToEndFlow_Works()
    {
        await using var host = await BudgetApiIntegrationTestHost.StartAsync();
        var client = await host.CreateAuthenticatedClientAsync();

        var categories = await client.GetFromJsonAsync<IReadOnlyList<BudgetCategoryResponse>>("/api/budget/categories");
        Assert.NotNull(categories);
        var category = categories.First(x => x.Section == "COSTS");

        const int year = 2026;
        const int month = 4;

        var annualRequest = new UpdateAnnualPlanRequest(
            [
                new AnnualCellInput(category.Id, Month: month, PlannedAmount: 1500m)
            ]);
        var annualUpsert = await client.PutAsJsonAsync($"/api/budget/annual/{year}", annualRequest);
        annualUpsert.EnsureSuccessStatusCode();

        var generateResponse = await client.PostAsync($"/api/budget/months/{year}/{month}/generate", null);
        generateResponse.EnsureSuccessStatusCode();
        var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateMonthlyActionsResponse>();
        Assert.NotNull(generated);
        Assert.True(generated.Total >= 1);

        var workspace = await client.GetFromJsonAsync<MonthlyWorkspaceResponse>($"/api/budget/months/{year}/{month}");
        Assert.NotNull(workspace);
        Assert.True(workspace.Actions.Count >= 1);

        var action = workspace.Actions.Single(x => x.CategoryId == category.Id);
        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/budget/months/{year}/{month}/actions/{action.ActionId}",
            new UpdateMonthlyActionRequest(Status: "DONE", ActualAmount: null));
        patchResponse.EnsureSuccessStatusCode();

        var updated = await patchResponse.Content.ReadFromJsonAsync<MonthlyActionResponse>();
        Assert.NotNull(updated);
        Assert.Equal("DONE", updated.Status);
        Assert.Equal(updated.PlannedAmount, updated.ActualAmount);
    }

    [Fact]
    public async Task UpdateMonthlyAction_DoneWithExplicitActual_KeepsProvidedAmount()
    {
        await using var host = await BudgetApiIntegrationTestHost.StartAsync();
        var client = await host.CreateAuthenticatedClientAsync();

        var categories = await client.GetFromJsonAsync<IReadOnlyList<BudgetCategoryResponse>>("/api/budget/categories");
        Assert.NotNull(categories);
        var category = categories.First(x => x.Section == "COSTS");

        const int year = 2026;
        const int month = 5;
        const decimal plannedAmount = 1500m;
        const decimal actualAmount = 987.65m;

        var annualRequest = new UpdateAnnualPlanRequest(
            [
                new AnnualCellInput(category.Id, Month: month, PlannedAmount: plannedAmount)
            ]);
        var annualUpsert = await client.PutAsJsonAsync($"/api/budget/annual/{year}", annualRequest);
        annualUpsert.EnsureSuccessStatusCode();

        var generateResponse = await client.PostAsync($"/api/budget/months/{year}/{month}/generate", null);
        generateResponse.EnsureSuccessStatusCode();

        var workspace = await client.GetFromJsonAsync<MonthlyWorkspaceResponse>($"/api/budget/months/{year}/{month}");
        Assert.NotNull(workspace);
        var action = workspace.Actions.Single(x => x.CategoryId == category.Id);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/budget/months/{year}/{month}/actions/{action.ActionId}",
            new UpdateMonthlyActionRequest(Status: "DONE", ActualAmount: actualAmount));
        patchResponse.EnsureSuccessStatusCode();

        var updated = await patchResponse.Content.ReadFromJsonAsync<MonthlyActionResponse>();
        Assert.NotNull(updated);
        Assert.Equal("DONE", updated.Status);
        Assert.Equal(actualAmount, updated.ActualAmount);
        Assert.NotEqual(updated.PlannedAmount, updated.ActualAmount);

        var workspaceAfterPatch = await client.GetFromJsonAsync<MonthlyWorkspaceResponse>($"/api/budget/months/{year}/{month}");
        Assert.NotNull(workspaceAfterPatch);

        var updatedFromWorkspace = workspaceAfterPatch.Actions.Single(x => x.ActionId == action.ActionId);
        Assert.Equal(actualAmount, updatedFromWorkspace.ActualAmount);
        Assert.Equal("DONE", updatedFromWorkspace.Status);
    }
}
