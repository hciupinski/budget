using System.Security.Claims;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Services;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetAssetsEndpoints
{
    public static RouteGroupBuilder MapBudgetAssetsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/assets/overview", async (
            int? year,
            int? month,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var targetYear = year ?? now.Year;
            var targetMonth = month ?? now.Month;
            var result = await service.GetAssetsOverviewAsync(targetYear, targetMonth, ct);
            return Results.Ok(result);
        });

        group.MapGet("/assets/accounts-overview", async (
            int? year,
            int? month,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var targetYear = year ?? now.Year;
            var targetMonth = month ?? now.Month;
            var result = await service.GetAccountsOverviewAsync(targetYear, targetMonth, ct);
            return Results.Ok(result);
        });

        group.MapGet("/assets/investments", async (
            string? refreshMode,
            ClaimsPrincipal user,
            IInvestmentsService service,
            CancellationToken ct) =>
        {
            var result = await service.GetInvestmentsAsync(BudgetEndpointUser.CurrentUser(user), refreshMode, ct);
            return Results.Ok(result);
        });

        group.MapPost("/assets/accounts", async (
            CreateBudgetAccountRequest request,
            ClaimsPrincipal user,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAccountAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/assets/accounts/{accountId:guid}", async (
            Guid accountId,
            UpdateBudgetAccountRequest request,
            ClaimsPrincipal user,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAccountAsync(accountId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/assets/transfers", async (
            CreateAccountTransferRequest request,
            ClaimsPrincipal user,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateTransferAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPut("/assets/snapshots/{year:int}/{month:int}", async (
            int year,
            int month,
            UpsertMonthlyAccountSnapshotsRequest request,
            ClaimsPrincipal user,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var result = await service.UpsertAccountSnapshotsAsync(year, month, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/assets/holdings", async (
            CreateInvestmentHoldingRequest request,
            ClaimsPrincipal user,
            IInvestmentsService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateHoldingAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/assets/holdings/{holdingId:guid}", async (
            Guid holdingId,
            UpdateInvestmentHoldingRequest request,
            ClaimsPrincipal user,
            IInvestmentsService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateHoldingAsync(holdingId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapDelete("/assets/holdings/{holdingId:guid}", async (
            Guid holdingId,
            ClaimsPrincipal user,
            IInvestmentsService service,
            CancellationToken ct) =>
        {
            await service.DeleteHoldingAsync(holdingId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapPost("/assets/prices/refresh", async (
            ClaimsPrincipal user,
            IInvestmentsService service,
            CancellationToken ct) =>
        {
            var result = await service.RefreshInvestmentPricesAsync(BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/assets/savings-goals", async (
            CreateSavingsGoalRequest request,
            ClaimsPrincipal user,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateSavingsGoalAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/assets/savings-goals/{goalId:guid}", async (
            Guid goalId,
            UpdateSavingsGoalRequest request,
            ClaimsPrincipal user,
            IAccountsService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateSavingsGoalAsync(goalId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        return group;
    }
}
