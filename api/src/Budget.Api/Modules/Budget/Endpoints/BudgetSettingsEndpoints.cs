using System.Security.Claims;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Services;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetSettingsEndpoints
{
    public static RouteGroupBuilder MapBudgetSettingsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (ISettingsService service, CancellationToken ct) =>
        {
            var categories = await service.GetCategoriesAsync(ct);
            return Results.Ok(categories);
        });

        group.MapGet("/settings/sections", async (ISettingsService service, CancellationToken ct) =>
        {
            var result = await service.GetManagedSectionsAsync(ct);
            return Results.Ok(result);
        });

        group.MapPut("/settings/sections", async (
            UpdateManagedSectionsRequest request,
            ClaimsPrincipal user,
            ISettingsService service,
            CancellationToken ct) =>
        {
            var result = await service.SaveManagedSectionsAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapGet("/settings/planner-customization", async (ISettingsService service, CancellationToken ct) =>
        {
            var result = await service.GetPlannerCustomizationAsync(ct);
            return Results.Ok(result);
        });

        group.MapPut("/settings/planner-customization", async (
            UpdatePlannerCustomizationRequest request,
            ClaimsPrincipal user,
            ISettingsService service,
            CancellationToken ct) =>
        {
            var result = await service.SavePlannerCustomizationAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapGet("/settings/general", async (ISettingsService service, CancellationToken ct) =>
        {
            var result = await service.GetGeneralSettingsAsync(ct);
            return Results.Ok(result);
        });

        group.MapPut("/settings/general", async (
            UpdateGeneralSettingsRequest request,
            ClaimsPrincipal user,
            ISettingsService service,
            CancellationToken ct) =>
        {
            var result = await service.SaveGeneralSettingsAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        return group;
    }
}
