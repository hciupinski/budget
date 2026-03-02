using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; }

    [Required]
    public string Audience { get; init; }

    [Required]
    public string Secret { get; init; }

    [Range(5, 1440)]
    public int ExpiresMinutes { get; init; } = 480;
}
