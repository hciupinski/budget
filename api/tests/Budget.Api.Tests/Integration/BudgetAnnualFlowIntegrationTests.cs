using System.Net.Http.Json;
using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Tests.Integration;

public sealed class BudgetAnnualFlowIntegrationTests
{
    [Fact]
    public async Task AnnualEndpoints_EndToEndFlow_Works()
    {
        await using var host = await BudgetApiIntegrationTestHost.StartAsync();
        var client = await host.CreateAuthenticatedClientAsync();

        var categories = await client.GetFromJsonAsync<IReadOnlyList<BudgetCategoryResponse>>("/api/budget/categories");
        Assert.NotNull(categories);
        var category = categories.First(x => x.Section == "INCOME");

        const int sourceYear = 2026;
        const int targetYear = 2027;
        var request = new UpdateAnnualPlanRequest(
            [
                new AnnualCellInput(category.Id, Month: 3, PlannedAmount: 12345.67m)
            ]);

        var upsertResponse = await client.PutAsJsonAsync($"/api/budget/annual/{sourceYear}", request);
        upsertResponse.EnsureSuccessStatusCode();

        var annual = await upsertResponse.Content.ReadFromJsonAsync<AnnualPlanResponse>();
        Assert.NotNull(annual);

        var sourceRow = annual.Categories.Single(x => x.CategoryId == category.Id);
        Assert.Equal(12345.67m, sourceRow.Months[2]);

        var copyResponse = await client.PostAsync($"/api/budget/annual/{targetYear}/copy-from/{sourceYear}", null);
        copyResponse.EnsureSuccessStatusCode();

        var copied = await copyResponse.Content.ReadFromJsonAsync<AnnualPlanResponse>();
        Assert.NotNull(copied);

        var targetRow = copied.Categories.Single(x => x.CategoryId == category.Id);
        Assert.Equal(12345.67m, targetRow.Months[2]);
    }
}
