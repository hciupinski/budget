using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    public string Secret { get; init; } = string.Empty;

    [Range(5, 1440)]
    public int ExpiresMinutes { get; init; } = 480;
}
