using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Auth;
using Budget.Api.Modules.Budget;
using Budget.Api.Modules.Budget.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Budget.Api.Tests.Integration;

public sealed class BudgetApiIntegrationTestHost : IAsyncDisposable
{
    private const string OwnerEmail = "owner.integration@example.com";
    private const string OwnerPassword = "integration-password-123";

    private readonly WebApplication _app;

    private BudgetApiIntegrationTestHost(WebApplication app)
    {
        _app = app;
    }

    public static async Task<BudgetApiIntegrationTestHost> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=budget-tests;Username=test;Password=test",
            ["OwnerAccount:Email"] = OwnerEmail,
            ["OwnerAccount:Password"] = OwnerPassword,
            ["OwnerAccount:PasswordHash"] = "",
            ["OwnerAccount:AllowLegacyPlaintextPassword"] = "true",
            ["Jwt:Issuer"] = "budget-tests",
            ["Jwt:Audience"] = "budget-tests",
            ["Jwt:Secret"] = "integration-tests-secret-with-min-32-chars",
            ["Jwt:ExpiresMinutes"] = "120"
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth-login", context =>
            {
                var partitionKey = context.Request.Headers["X-Login-Email"].ToString().Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(partitionKey))
                {
                    partitionKey = "integration-tests";
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        builder.Services.AddAuthModule(builder.Configuration);

        var databaseName = $"budget-api-it-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<BudgetDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
        });
        builder.Services.AddScoped<BudgetDbInitializer>();
        builder.Services.AddScoped<ISettingsService, SettingsService>();
        builder.Services.AddScoped<IAnnualPlanService, AnnualPlanService>();
        builder.Services.AddScoped<IMonthlyWorkspaceService, MonthlyWorkspaceService>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IAccountsService, AccountsService>();
        builder.Services.AddScoped<IInvestmentsService, InvestmentsService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddOptions<MarketPricesOptions>()
            .Bind(builder.Configuration.GetSection(MarketPricesOptions.SectionName))
            .ValidateDataAnnotations();
        builder.Services.AddSingleton<IMarketPriceService, MarketPriceService>();
        builder.Services.AddHttpClient("fx-rates");
        builder.Services.AddHttpClient("market-prices");

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        var app = builder.Build();

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var (statusCode, title) = exception switch
                {
                    ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                    KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                    DbUpdateException => (StatusCodes.Status400BadRequest, "Database update failed"),
                    _ => (StatusCodes.Status500InternalServerError, "Budget operation failed")
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var detail = exception?.Message ?? "An unexpected error occurred.";
                var problem = new ProblemDetails
                {
                    Title = title,
                    Detail = detail,
                    Status = statusCode
                };
                problem.Extensions["error"] = detail;
                problem.Extensions["traceId"] = context.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(problem);
            });
        });

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/api/ping", (ClaimsPrincipal user) => Results.Ok(new
        {
            status = "ok",
            user = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "owner"
        }));

        app.MapAuthModule();
        app.MapBudgetModule();

        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<BudgetDbInitializer>();
            await initializer.InitializeAsync(CancellationToken.None);
        }

        await app.StartAsync();

        return new BudgetApiIntegrationTestHost(app);
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string? emailHeader = null)
    {
        var client = _app.GetTestClient();
        var token = await LoginAsync(client, emailHeader);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client, string? emailHeader)
    {
        var headerValue = emailHeader ?? OwnerEmail;
        client.DefaultRequestHeaders.Remove("X-Login-Email");
        client.DefaultRequestHeaders.Add("X-Login-Email", headerValue);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(OwnerEmail, OwnerPassword));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));

        return payload.AccessToken;
    }
}
