using Microsoft.AspNetCore.Identity;
using OpsHub.Models;

namespace OpsHub.Services;

public sealed class IdentityBootstrapper(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<IdentityBootstrapper> logger)
{
    private static readonly string[] Roles = ["Admin", "Agent", "Requester"];

    public async Task InitializeAsync()
    {
        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create the {role} role: {FormatErrors(result.Errors)}");
        }

        var email = configuration["Identity:BootstrapAdmin:Email"];
        var password = configuration["Identity:BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No bootstrap administrator was configured. Set Identity__BootstrapAdmin__Email and Identity__BootstrapAdmin__Password to create one.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Both Identity__BootstrapAdmin__Email and Identity__BootstrapAdmin__Password are required when bootstrapping an administrator.");

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = email,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create the bootstrap administrator: {FormatErrors(createResult.Errors)}");
        }

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            logger.LogInformation("The configured bootstrap administrator already exists.");
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
            throw new InvalidOperationException(
                $"Could not assign the Admin role to the bootstrap user: {FormatErrors(roleResult.Errors)}");

        logger.LogInformation("Created or promoted the configured bootstrap administrator.");
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors) =>
        string.Join("; ", errors.Select(error => error.Description));
}
