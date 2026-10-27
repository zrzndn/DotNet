using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpsHub.Controllers;
using OpsHub.Data;
using OpsHub.Models;
using OpsHub.Services;

namespace OpsHub.Tests;

public sealed class TicketExportControllerTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private TicketExportController controller = null!;
    private OpsHubDbContext db = null!;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OpsHubDbContext>()
            .UseSqlite(connection)
            .Options;
        db = new OpsHubDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(options);
        var ticketService = new TicketService(
            factory,
            new CurrentUserContext(new ThrowingAuthenticationStateProvider()),
            new SlaCalculator(new SlaOptions
            {
                ResolutionTargetHours = new Dictionary<string, double>
                {
                    ["Low"] = 120,
                    ["Medium"] = 72,
                    ["High"] = 24,
                    ["Critical"] = 4
                },
                AtRiskWindowHours = 2
            }));
        controller = new TicketExportController(ticketService, new TicketCsvExporter());
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ExportTickets_UsesMvcPrincipalAndReturnsCsvForAgent()
    {
        db.SupportTickets.Add(Ticket("VPN reset", "owner-1"));
        await db.SaveChangesAsync();
        SetPrincipal(CreatePrincipal("Agent", "agent-1"));

        var result = await controller.ExportTickets();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("opshub-tickets.csv", file.FileDownloadName);
        Assert.Contains("VPN reset", Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public async Task ExportTickets_RejectsUnauthorizedPrincipal()
    {
        SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => controller.ExportTickets());
    }

    [Fact]
    public async Task ExportTickets_LimitsRequesterToOwnedActiveTickets()
    {
        db.SupportTickets.AddRange(
            Ticket("My request", "requester-1"),
            Ticket("Another request", "requester-2"),
            Ticket("Archived request", "requester-1", isArchived: true));
        await db.SaveChangesAsync();
        SetPrincipal(CreatePrincipal("Requester", "requester-1"));

        var result = await controller.ExportTickets();

        var file = Assert.IsType<FileContentResult>(result);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("My request", csv);
        Assert.DoesNotContain("Another request", csv);
        Assert.DoesNotContain("Archived request", csv);
    }

    private void SetPrincipal(ClaimsPrincipal principal)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string role, string userId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"{userId}@example.test"),
            new Claim(ClaimTypes.Role, role)
        ], "test"));

    private static SupportTicket Ticket(
        string title,
        string ownerId,
        bool isArchived = false) => new()
    {
        Title = title,
        Description = "A test support request.",
        Category = "Network",
        Requester = ownerId,
        Priority = "Medium",
        Status = "Open",
        OwnerUserId = ownerId,
        IsArchived = isArchived
    };

    private sealed class TestDbContextFactory(DbContextOptions<OpsHubDbContext> options)
        : IDbContextFactory<OpsHubDbContext>
    {
        public OpsHubDbContext CreateDbContext() => new(options);

        public Task<OpsHubDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class ThrowingAuthenticationStateProvider
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            throw new InvalidOperationException(
                "The Razor authentication-state provider must not be used by MVC requests.");
    }
}
