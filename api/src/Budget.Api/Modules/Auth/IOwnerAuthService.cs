namespace Budget.Api.Modules.Auth;

public interface IOwnerAuthService
{
    OwnerCredentialValidationResult ValidateCredentials(string email, string password);
}
