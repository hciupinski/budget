using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Auth;

public sealed class OwnerAccountOptions
{
    public const string SectionName = "OwnerAccount";

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    // Temporary compatibility field for legacy plaintext credentials.
    [MinLength(12)]
    public string? Password { get; init; }

    // Preferred production-safe credential format.
    public string? PasswordHash { get; init; }

    public bool AllowLegacyPlaintextPassword { get; init; }
}
