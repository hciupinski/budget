using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Budget.Api.Modules.Auth;

public interface IOwnerAuthService
{
    bool ValidateCredentials(string email, string password);
}

public sealed class OwnerAuthService(IOptions<OwnerAccountOptions> options) : IOwnerAuthService
{
    private readonly OwnerAccountOptions _owner = options.Value;

    public bool ValidateCredentials(string email, string password)
    {
        return FixedTimeEquals(_owner.Email, email) && FixedTimeEquals(_owner.Password, password);
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
