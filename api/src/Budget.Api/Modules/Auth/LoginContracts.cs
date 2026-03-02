using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(1)] string Password);

public sealed record LoginResponse(string AccessToken, string TokenType);
