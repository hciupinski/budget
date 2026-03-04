namespace Budget.Api.Modules.Auth;

public sealed record OwnerCredentialValidationResult(bool IsValid, bool UsedLegacyPlaintextPassword)
{
    public static readonly OwnerCredentialValidationResult Invalid = new(false, false);
}
