using System.Security.Claims;
using Budget.Api.Modules.Auth;
using Budget.Api.Modules.Budget;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddBudgetModule(builder.Configuration);

var allowedOrigins = builder.Configuration["AllowedOrigins"]?
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

app.UseHttpLogging();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "api",
    utcTime = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapHealthChecks("/health/live").AllowAnonymous();

app.MapGet("/api/ping", (ClaimsPrincipal user) => Results.Ok(new
{
    status = "ok",
    user = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "owner"
}));

app.MapAuthModule();
app.MapBudgetModule();

await app.Services.InitializeBudgetDatabaseAsync(CancellationToken.None);
await app.RunAsync();

public partial class Program;
