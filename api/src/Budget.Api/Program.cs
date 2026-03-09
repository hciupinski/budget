using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Budget.Api.Modules.Auth;
using Budget.Api.Modules.Budget;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-login", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var emailHint = httpContext.Request.Headers["X-Login-Email"].ToString().Trim().ToLowerInvariant();
        var partitionKey = string.IsNullOrWhiteSpace(emailHint)
            ? $"{ipAddress}:unknown"
            : $"{ipAddress}:{emailHint}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = "Too many requests",
            Detail = "Too many login attempts. Please wait a minute and try again.",
            Status = StatusCodes.Status429TooManyRequests
        };
        problem.Extensions["error"] = problem.Detail;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);
    };
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

app.UseHttpLogging();
app.UseCors("web");
app.UseRateLimiter();
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
