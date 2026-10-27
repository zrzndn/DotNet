using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace OpsHub.Services;

public sealed class CurrentUserContext(AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<ClaimsPrincipal> GetPrincipalAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User;
    }

    public async Task<string> GetRequiredUserIdAsync()
    {
        var principal = await GetPrincipalAsync();
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Sign in to perform this action.");
    }

    public async Task<string> GetRequiredUserNameAsync()
    {
        var principal = await GetPrincipalAsync();
        return principal.Identity?.Name
            ?? throw new UnauthorizedAccessException("Sign in to perform this action.");
    }
}
