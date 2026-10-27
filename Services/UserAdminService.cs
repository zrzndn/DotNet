using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsHub.Models;

namespace OpsHub.Services;

public sealed record ManagedUser(
    string Id,
    string Email,
    string DisplayName,
    string Role);

public sealed class UserAdminService(
    UserManager<ApplicationUser> userManager,
    CurrentUserContext currentUser)
{
    private static readonly string[] Roles = ["Admin", "Agent", "Requester"];

    public async Task<IReadOnlyList<ManagedUser>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync();
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var managedUsers = new List<ManagedUser>(users.Count);
        foreach (var user in users)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            managedUsers.Add(new ManagedUser(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                Roles.FirstOrDefault(userRoles.Contains) ?? "No role"));
        }

        return managedUsers;
    }

    public async Task CreateUserAsync(
        string? email,
        string? displayName,
        string? password,
        string? role,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email) ||
            !TicketRules.IsValidRequester(displayName) ||
            string.IsNullOrWhiteSpace(password) ||
            !Roles.Contains(role, StringComparer.Ordinal))
            throw new ArgumentException("Enter a valid email, display name, password, and role.");

        var normalizedDisplayName = displayName!.Trim();
        var assignedRole = role!;
        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = normalizedDisplayName,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            throw new ArgumentException(FormatErrors(createResult.Errors));

        var roleResult = await userManager.AddToRoleAsync(user, assignedRole);
        if (roleResult.Succeeded)
            return;

        var cleanupResult = await userManager.DeleteAsync(user);
        var cleanupError = cleanupResult.Succeeded
            ? string.Empty
            : $" The account could not be rolled back: {FormatErrors(cleanupResult.Errors)}";
        throw new InvalidOperationException(
            $"Could not assign the selected role: {FormatErrors(roleResult.Errors)}.{cleanupError}");
    }

    public async Task ChangeRoleAsync(
        string? userId,
        string? role,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userId) ||
            !Roles.Contains(role, StringComparer.Ordinal))
            throw new ArgumentException("Choose a valid user and role.");

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("The selected user was not found.");
        var currentRoles = await userManager.GetRolesAsync(user);

        if (currentRoles.Contains("Admin", StringComparer.Ordinal) &&
            role != "Admin" &&
            await GetAdministratorCountAsync() <= 1)
            throw new InvalidOperationException("The last administrator cannot be demoted.");

        var removeResult = await userManager.RemoveFromRolesAsync(
            user,
            currentRoles.Where(Roles.Contains));
        if (!removeResult.Succeeded)
            throw new InvalidOperationException(FormatErrors(removeResult.Errors));

        var addResult = await userManager.AddToRoleAsync(user, role!);
        if (addResult.Succeeded)
            return;

        var restoreResults = new List<IdentityResult>();
        foreach (var currentRole in currentRoles.Where(Roles.Contains))
            restoreResults.Add(await userManager.AddToRoleAsync(user, currentRole));

        var restoreErrors = restoreResults
            .Where(result => !result.Succeeded)
            .SelectMany(result => result.Errors);
        var restoreError = FormatErrors(restoreErrors);
        throw new InvalidOperationException(
            $"Could not assign the selected role: {FormatErrors(addResult.Errors)}." +
            (string.IsNullOrEmpty(restoreError) ? "" : $" Role restoration failed: {restoreError}"));
    }

    private async Task RequireAdministratorAsync()
    {
        var principal = await currentUser.GetPrincipalAsync();
        if (principal.Identity?.IsAuthenticated != true ||
            !principal.IsInRole("Admin"))
            throw new UnauthorizedAccessException("Administrator access is required.");
    }

    private async Task<int> GetAdministratorCountAsync()
    {
        var administrators = await userManager.GetUsersInRoleAsync("Admin");
        return administrators.Count;
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors) =>
        string.Join(" ", errors.Select(error => error.Description));
}
