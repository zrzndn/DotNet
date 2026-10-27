using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpsHub.Data;
using OpsHub.Models;
using OpsHub.Services;

namespace OpsHub.Tests;

public sealed class IdentityTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private ServiceProvider services = null!;
    private AsyncServiceScope scope;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddDbContext<OpsHubDbContext>(
            options => options.UseSqlite(connection));
        serviceCollection
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<OpsHubDbContext>();

        services = serviceCollection.BuildServiceProvider();
        scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsHubDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await scope.DisposeAsync();
        await services.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Bootstrapper_CreatesRolesAndConfiguredAdmin()
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var bootstrapper = new IdentityBootstrapper(
            roleManager,
            userManager,
            Configuration(
                ("Identity:BootstrapAdmin:Email", "admin@example.test"),
                ("Identity:BootstrapAdmin:Password", "SecureOpsHub!2026Strong")),
            scope.ServiceProvider.GetRequiredService<ILogger<IdentityBootstrapper>>());

        await bootstrapper.InitializeAsync();

        var admin = await userManager.FindByEmailAsync("admin@example.test");
        Assert.NotNull(admin);
        Assert.True(await userManager.CheckPasswordAsync(admin, "SecureOpsHub!2026Strong"));
        Assert.False(await userManager.CheckPasswordAsync(admin, "incorrect"));
        Assert.True(await userManager.IsInRoleAsync(admin, "Admin"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.True(await roleManager.RoleExistsAsync("Agent"));
        Assert.True(await roleManager.RoleExistsAsync("Requester"));
    }

    [Fact]
    public async Task Bootstrapper_RejectsPartialAdminConfiguration()
    {
        var bootstrapper = new IdentityBootstrapper(
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            Configuration(("Identity:BootstrapAdmin:Email", "admin@example.test")),
            scope.ServiceProvider.GetRequiredService<ILogger<IdentityBootstrapper>>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bootstrapper.InitializeAsync());
    }

    [Fact]
    public async Task UserAdministration_CreatesUsersWithRequestedRole()
    {
        await InitializeAdminAsync();
        var adminService = CreateAdminService();

        await adminService.CreateUserAsync(
            "agent@example.test",
            "Alex Agent",
            "SecureAgent!2026Password",
            "Agent");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("agent@example.test");
        Assert.NotNull(user);
        Assert.Equal("Alex Agent", user.DisplayName);
        Assert.True(await userManager.IsInRoleAsync(user, "Agent"));
        Assert.False(await userManager.IsInRoleAsync(user, "Admin"));
        Assert.Contains(await adminService.GetUsersAsync(),
            item => item.Email == "agent@example.test" && item.Role == "Agent");
    }

    [Fact]
    public async Task UserAdministration_RejectsNonAdminAccess()
    {
        await InitializeAdminAsync();
        var requester = CreatePrincipal("Requester", "requester-1", "requester@example.test");
        var service = new UserAdminService(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            new CurrentUserContext(new TestAuthenticationStateProvider(requester)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetUsersAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateUserAsync(
                "attacker@example.test",
                "Attacker",
                "SecureAgent!2026Password",
                "Agent"));
    }

    [Fact]
    public async Task UserAdministration_PreventsDemotingLastAdmin()
    {
        await InitializeAdminAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync("admin@example.test");
        Assert.NotNull(admin);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAdminService().ChangeRoleAsync(admin.Id, "Agent"));

        Assert.True(await userManager.IsInRoleAsync(admin, "Admin"));
    }

    private async Task InitializeAdminAsync()
    {
        var bootstrapper = new IdentityBootstrapper(
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            Configuration(
                ("Identity:BootstrapAdmin:Email", "admin@example.test"),
                ("Identity:BootstrapAdmin:Password", "SecureOpsHub!2026Strong")),
            scope.ServiceProvider.GetRequiredService<ILogger<IdentityBootstrapper>>());
        await bootstrapper.InitializeAsync();
    }

    private UserAdminService CreateAdminService() =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            new CurrentUserContext(new TestAuthenticationStateProvider(
                CreatePrincipal("Admin", "admin-id", "admin@example.test"))));

    private static IConfiguration Configuration(
        params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(
                item => item.Key,
                item => (string?)item.Value))
            .Build();

    private static ClaimsPrincipal CreatePrincipal(
        string role,
        string id,
        string name) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role)
        ], "test"));

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }
}
