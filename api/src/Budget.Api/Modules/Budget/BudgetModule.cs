using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Endpoints;
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
        services.AddScoped<BudgetDatabaseMigrator>();

        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAnnualPlanService, AnnualPlanService>();
        services.AddScoped<IMonthlyWorkspaceService, MonthlyWorkspaceService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAccountsService, AccountsService>();
        services.AddScoped<IInvestmentsService, InvestmentsService>();

        services.AddMemoryCache();
        services.AddOptions<MarketPricesOptions>()
            .Bind(configuration.GetSection(MarketPricesOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddSingleton<IMarketPriceService, MarketPriceService>();
        services.AddHttpClient("fx-rates");
        services.AddHttpClient("market-prices");

        return services;
    }

    public static async Task InitializeBudgetDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<BudgetDatabaseMigrator>();
        await migrator.MigrateAsync(cancellationToken);

        var initializer = scope.ServiceProvider.GetRequiredService<BudgetDbInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }

    public static IEndpointRouteBuilder MapBudgetModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/budget")
            .WithTags("Budget");

        group.MapBudgetSettingsEndpoints();
        group.MapBudgetAnnualPlanEndpoints();
        group.MapBudgetMonthlyWorkspaceEndpoints();
        group.MapBudgetAuditEndpoints();
        group.MapBudgetAssetsEndpoints();

        return app;
    }
}
