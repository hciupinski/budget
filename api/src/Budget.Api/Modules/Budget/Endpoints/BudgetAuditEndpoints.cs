using Budget.Api.Modules.Budget.Services;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetAuditEndpoints
{
    public static RouteGroupBuilder MapBudgetAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/audit", async (
            int? year,
            int? month,
            int? limit,
            IAuditService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAuditAsync(year, month, limit ?? 50, ct);
            return Results.Ok(result);
        });

        return group;
    }
}
