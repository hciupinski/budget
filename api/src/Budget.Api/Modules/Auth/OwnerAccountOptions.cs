using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Auth;

public sealed class OwnerAccountOptions
{
    public const string SectionName = "OwnerAccount";

    [Required]
    [EmailAddress]
    public string Email { get; init; }

    [Required]
    [MinLength(12)]
    public string Password { get; init; }
}
