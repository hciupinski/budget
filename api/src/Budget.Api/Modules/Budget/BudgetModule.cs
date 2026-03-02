using System.Security.Claims;

namespace Budget.Api.Modules.Budget;

public static class BudgetModule
{
    public static IEndpointRouteBuilder MapBudgetModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/budget")
            .WithTags("Budget");

        group.MapGet("/summary", (ClaimsPrincipal user) =>
        {
            var ownerEmail = user.FindFirstValue(ClaimTypes.Email) ?? "owner";
            return Results.Ok(new
            {
                ownerEmail,
                month = DateOnly.FromDateTime(DateTime.UtcNow),
                totals = new
                {
                    incomePlanned = 0,
                    costsPlanned = 0,
                    savingsPlanned = 0,
                    remainderPlanned = 0
                },
                status = "foundation_ready"
            });
        });

        return app;
    }
}
