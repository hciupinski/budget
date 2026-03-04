using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Budget.Api.Modules.Auth;

public sealed class OwnerAuthService(
    IOptions<OwnerAccountOptions> options,
    IPasswordHasher<object> passwordHasher,
    ILogger<OwnerAuthService> logger)
    : IOwnerAuthService
{
    private static readonly object PasswordHasherUser = new();
    private readonly OwnerAccountOptions _owner = options.Value;

    public OwnerCredentialValidationResult ValidateCredentials(string email, string password)
    {
        if (!FixedTimeEquals(_owner.Email, email))
        {
            return OwnerCredentialValidationResult.Invalid;
        }

        var legacyEnabled = _owner.AllowLegacyPlaintextPassword && !string.IsNullOrWhiteSpace(_owner.Password);

        if (!string.IsNullOrWhiteSpace(_owner.PasswordHash))
        {
            var verificationResult = passwordHasher.VerifyHashedPassword(PasswordHasherUser, _owner.PasswordHash, password);
            if (verificationResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
            {
                return new OwnerCredentialValidationResult(true, false);
            }

            if (!legacyEnabled)
            {
                return OwnerCredentialValidationResult.Invalid;
            }
        }

        if (legacyEnabled && FixedTimeEquals(_owner.Password ?? string.Empty, password))
        {
            logger.LogWarning("Legacy plaintext owner password verification path is active. Configure OwnerAccount:PasswordHash and disable OwnerAccount:AllowLegacyPlaintextPassword.");
            return new OwnerCredentialValidationResult(true, true);
        }

        return OwnerCredentialValidationResult.Invalid;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
