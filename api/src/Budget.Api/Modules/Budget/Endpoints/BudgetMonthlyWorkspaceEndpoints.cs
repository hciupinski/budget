using System.Security.Claims;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Services;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetMonthlyWorkspaceEndpoints
{
    public static RouteGroupBuilder MapBudgetMonthlyWorkspaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/months/{year:int}/{month:int}/generate", async (
            int year,
            int month,
            ClaimsPrincipal user,
            IMonthlyWorkspaceService service,
            CancellationToken ct) =>
        {
            var result = await service.GenerateMonthlyActionsAsync(year, month, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapGet("/months/{year:int}/{month:int}", async (
            int year,
            int month,
            string? status,
            IMonthlyWorkspaceService service,
            CancellationToken ct) =>
        {
            var result = await service.GetMonthlyWorkspaceAsync(year, month, status, ct);
            return Results.Ok(result);
        });

        group.MapPatch("/months/{year:int}/{month:int}/actions/{actionId:guid}", async (
            int year,
            int month,
            Guid actionId,
            UpdateMonthlyActionRequest request,
            ClaimsPrincipal user,
            IMonthlyWorkspaceService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateMonthlyActionAsync(
                year,
                month,
                actionId,
                request,
                BudgetEndpointUser.CurrentUser(user),
                ct);
            return Results.Ok(result);
        });

        return group;
    }
}
