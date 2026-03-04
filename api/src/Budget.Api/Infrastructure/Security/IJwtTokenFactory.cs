namespace Budget.Api.Infrastructure.Security;

public interface IJwtTokenFactory
{
    string CreateToken(string email);
}
