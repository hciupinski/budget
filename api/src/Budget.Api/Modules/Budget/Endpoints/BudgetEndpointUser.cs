using System.Security.Claims;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetEndpointUser
{
    public static string CurrentUser(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.Identity?.Name
            ?? "owner";
    }
}
