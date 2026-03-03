using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Services;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget;

public static class BudgetModule
{
    public static IServiceCollection AddBudgetModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found.");

        services.AddDbContext<BudgetDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<BudgetDbInitializer>();
        services.AddScoped<BudgetService>();
        services.AddHttpClient("fx-rates");
        services.AddHttpClient("market-prices");

        return services;
    }

    public static async Task InitializeBudgetDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<BudgetDbInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }

    public static IEndpointRouteBuilder MapBudgetModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/budget")
            .WithTags("Budget");

        group.MapGet("/categories", async (BudgetService service, CancellationToken ct) =>
        {
            var categories = await service.GetCategoriesAsync(ct);
            return Results.Ok(categories);
        });

        group.MapGet("/settings/sections", async (BudgetService service, CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetManagedSectionsAsync(ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPut("/settings/sections", async (
            UpdateManagedSectionsRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.SaveManagedSectionsAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapGet("/settings/planner-customization", async (BudgetService service, CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetPlannerCustomizationAsync(ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPut("/settings/planner-customization", async (
            UpdatePlannerCustomizationRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.SavePlannerCustomizationAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapGet("/settings/general", async (BudgetService service, CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetGeneralSettingsAsync(ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPut("/settings/general", async (
            UpdateGeneralSettingsRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.SaveGeneralSettingsAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapGet("/annual/{year:int}", async (int year, BudgetService service, CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetAnnualPlanAsync(year, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPut("/annual/{year:int}", async (
            int year,
            UpdateAnnualPlanRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.UpsertAnnualPlanAsync(year, request, CurrentUser(user), ct);
                var result = await service.GetAnnualPlanAsync(year, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/annual/{year:int}/copy-from/{sourceYear:int}", async (
            int year,
            int sourceYear,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.CopyAnnualPlanAsync(year, sourceYear, CurrentUser(user), ct);
                var result = await service.GetAnnualPlanAsync(year, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/months/{year:int}/{month:int}/generate", async (
            int year,
            int month,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GenerateMonthlyActionsAsync(year, month, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapGet("/months/{year:int}/{month:int}", async (
            int year,
            int month,
            string? status,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetMonthlyWorkspaceAsync(year, month, status, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPatch("/months/{year:int}/{month:int}/actions/{actionId:guid}", async (
            int year,
            int month,
            Guid actionId,
            UpdateMonthlyActionRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpdateMonthlyActionAsync(year, month, actionId, request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapGet("/audit", async (
            int? year,
            int? month,
            int? limit,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetAuditAsync(year, month, limit ?? 50, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapGet("/assets/overview", async (
            int? year,
            int? month,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var targetYear = year ?? now.Year;
                var targetMonth = month ?? now.Month;
                var result = await service.GetAssetsOverviewAsync(targetYear, targetMonth, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/assets/accounts", async (
            CreateBudgetAccountRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateAccountAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPatch("/assets/accounts/{accountId:guid}", async (
            Guid accountId,
            UpdateBudgetAccountRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpdateAccountAsync(accountId, request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/assets/transfers", async (
            CreateAccountTransferRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateTransferAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPut("/assets/snapshots/{year:int}/{month:int}", async (
            int year,
            int month,
            UpsertMonthlyAccountSnapshotsRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpsertAccountSnapshotsAsync(year, month, request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/assets/holdings", async (
            CreateInvestmentHoldingRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateHoldingAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPatch("/assets/holdings/{holdingId:guid}", async (
            Guid holdingId,
            UpdateInvestmentHoldingRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpdateHoldingAsync(holdingId, request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapDelete("/assets/holdings/{holdingId:guid}", async (
            Guid holdingId,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.DeleteHoldingAsync(holdingId, CurrentUser(user), ct);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/assets/prices/refresh", async (
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RefreshInvestmentPricesAsync(CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPost("/assets/savings-goals", async (
            CreateSavingsGoalRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateSavingsGoalAsync(request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        group.MapPatch("/assets/savings-goals/{goalId:guid}", async (
            Guid goalId,
            UpdateSavingsGoalRequest request,
            ClaimsPrincipal user,
            BudgetService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.UpdateSavingsGoalAsync(goalId, request, CurrentUser(user), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return ToErrorResult(ex);
            }
        });

        return app;
    }

    private static string CurrentUser(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.Identity?.Name
            ?? "owner";
    }

    private static IResult ToErrorResult(Exception exception)
    {
        return exception switch
        {
            ValidationException validation => Results.BadRequest(new { error = validation.Message }),
            KeyNotFoundException notFound => Results.NotFound(new { error = notFound.Message }),
            DbUpdateException dbUpdate => Results.BadRequest(new { error = dbUpdate.Message }),
            _ => Results.Problem(title: "Budget operation failed", detail: exception.Message)
        };
    }
}
