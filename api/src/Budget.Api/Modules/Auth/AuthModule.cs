using Budget.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Budget.Api.Modules.Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddOptions<OwnerAccountOptions>()
            .Bind(configuration.GetSection(OwnerAccountOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.Secret.Length >= 32, "JWT secret must be at least 32 characters long.")
            .ValidateOnStart();

        services.AddSingleton<IOwnerAuthService, OwnerAuthService>();
        services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();

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

        group.MapPost("/login", (
                LoginRequest request,
                IOwnerAuthService authService,
                IJwtTokenFactory tokenFactory) =>
            {
                if (!authService.ValidateCredentials(request.Email, request.Password))
                {
                    return Results.Unauthorized();
                }

                var accessToken = tokenFactory.CreateToken(request.Email);
                return Results.Ok(new LoginResponse(accessToken, "Bearer"));
            })
            .AllowAnonymous();

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
}
