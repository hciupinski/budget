using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Infrastructure.Security;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Budget.Api.Modules.Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddOptions<OwnerAccountOptions>()
            .Bind(configuration.GetSection(OwnerAccountOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options =>
                {
                    var hasHash = !string.IsNullOrWhiteSpace(options.PasswordHash);
                    var legacyEnabled = options.AllowLegacyPlaintextPassword && !string.IsNullOrWhiteSpace(options.Password);
                    return hasHash || legacyEnabled;
                },
                "Configure OwnerAccount:PasswordHash (recommended) or enable temporary OwnerAccount:AllowLegacyPlaintextPassword with OwnerAccount:Password.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.Secret.Length >= 32, "JWT secret must be at least 32 characters long.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher<object>, PasswordHasher<object>>();
        services.AddSingleton<IOwnerAuthService, OwnerAuthService>();
        services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();
        services.AddHostedService<OwnerAccountStartupLogger>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
                };
            });

        return services;
    }

    public static IEndpointRouteBuilder MapAuthModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/login", async (
                LoginRequest request,
                HttpContext httpContext,
                IOwnerAuthService authService,
                IJwtTokenFactory tokenFactory,
                BudgetDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var validationResult = authService.ValidateCredentials(request.Email, request.Password);
                if (!validationResult.IsValid)
                {
                    await TryWriteLoginAuditAsync(
                        dbContext,
                        eventType: "LOGIN_FAILED",
                        changedBy: request.Email,
                        payload: new
                        {
                            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                            email = request.Email
                        },
                        cancellationToken);

                    return Results.Unauthorized();
                }

                await TryWriteLoginAuditAsync(
                    dbContext,
                    eventType: "LOGIN_SUCCEEDED",
                    changedBy: request.Email,
                    payload: new
                    {
                        ipAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                        email = request.Email,
                        usedLegacyPlaintextPassword = validationResult.UsedLegacyPlaintextPassword
                    },
                    cancellationToken);

                var accessToken = tokenFactory.CreateToken(request.Email);
                return Results.Ok(new LoginResponse(accessToken, "Bearer"));
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-login");

        group.MapPost("/logout", () => Results.Ok(new { status = "logged_out" }))
            .RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
            {
                email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "owner",
                role = user.FindFirstValue(ClaimTypes.Role) ?? "Owner"
            }))
            .RequireAuthorization();

        return app;
    }

    private static async Task TryWriteLoginAuditAsync(
        BudgetDbContext dbContext,
        string eventType,
        string changedBy,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "Auth",
                EntityId = Guid.NewGuid(),
                EventType = eventType,
                ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy,
                ChangedAt = DateTimeOffset.UtcNow,
                Payload = JsonSerializer.Serialize(payload)
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Never block auth flow on audit persistence failures.
        }
    }
}
