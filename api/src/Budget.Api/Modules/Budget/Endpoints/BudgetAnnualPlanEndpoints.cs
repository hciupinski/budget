using System.Security.Claims;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Services;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetAnnualPlanEndpoints
{
    public static RouteGroupBuilder MapBudgetAnnualPlanEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/annual/{year:int}", async (int year, IAnnualPlanService service, CancellationToken ct) =>
        {
            var result = await service.GetAnnualPlanAsync(year, ct);
            return Results.Ok(result);
        });

        group.MapPut("/annual/{year:int}", async (
            int year,
            UpdateAnnualPlanRequest request,
            ClaimsPrincipal user,
            IAnnualPlanService service,
            CancellationToken ct) =>
        {
            await service.UpsertAnnualPlanAsync(year, request, BudgetEndpointUser.CurrentUser(user), ct);
            var result = await service.GetAnnualPlanAsync(year, ct);
            return Results.Ok(result);
        });

        group.MapPost("/annual/{year:int}/copy-from/{sourceYear:int}", async (
            int year,
            int sourceYear,
            ClaimsPrincipal user,
            IAnnualPlanService service,
            CancellationToken ct) =>
        {
            await service.CopyAnnualPlanAsync(year, sourceYear, BudgetEndpointUser.CurrentUser(user), ct);
            var result = await service.GetAnnualPlanAsync(year, ct);
            return Results.Ok(result);
        });

        return group;
    }
}
