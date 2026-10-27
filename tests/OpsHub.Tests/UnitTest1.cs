
using OpsHub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using OpsHub.Data;
using OpsHub.Models;

namespace OpsHub.Tests;

public class TicketRulesTests
{
    [Theory]
    [InlineData("Laptop cannot connect to Wi-Fi", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidTitle_ChecksRequiredText(
        string? title, bool expected)
    {
        Assert.Equal(expected, TicketRules.IsValidTitle(title));
    }

    [Fact]
    public void IsValidTitle_RejectsMoreThan200Characters()
    {
        Assert.True(TicketRules.IsValidTitle(new string('a', 200)));
        Assert.False(TicketRules.IsValidTitle(new string('a', 201)));
    }

    [Theory]
    [InlineData("Hardware")]
    [InlineData("Network")]
    [InlineData("Software")]
    [InlineData("Access and Accounts")]
    public void IsValidCategory_AcceptsSupportedCategories(string category)
    {
        Assert.True(TicketRules.IsValidCategory(category));
    }

    [Fact]
    public void IsValidCategory_RejectsUnknownCategory()
    {
        Assert.False(TicketRules.IsValidCategory("Unknown"));
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Critical")]
    public void IsValidPriority_AcceptsSupportedPriorities(string priority)
    {
        Assert.True(TicketRules.IsValidPriority(priority));
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("In Progress")]
    [InlineData("Resolved")]
    public void IsValidStatus_AcceptsSupportedStatuses(string status)
    {
        Assert.True(TicketRules.IsValidStatus(status));
    }

    [Fact]
    public void IsValidStatus_RejectsUnknownStatus()
    {
        Assert.False(TicketRules.IsValidStatus("Closed"));
    }

    [Theory]
    [InlineData("Open", "In Progress", true)]
    [InlineData("Open", "Resolved", true)]
    [InlineData("Resolved", "Open", true)]
    [InlineData("Resolved", "In Progress", false)]
    [InlineData("Open", "Closed", false)]
    public void IsValidStatusTransition_EnforcesWorkflow(
        string current, string next, bool expected)
    {
        Assert.Equal(expected, TicketRules.IsValidStatusTransition(current, next));
    }
}

public sealed class TicketServiceTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private TicketService service = null!;
    private TestDbContextFactory factory = null!;
    private SlaCalculator slaCalculator = null!;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OpsHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var db = new OpsHubDbContext(options))
            await db.Database.EnsureCreatedAsync();

        factory = new TestDbContextFactory(options);
        slaCalculator = CreateSlaCalculator();
        service = CreateService("Agent", "agent-1", "agent@example.com");
    }

    public async Task DisposeAsync() => await connection.DisposeAsync();

    [Fact]
    public async Task CreateAsync_PersistsTicketWithOpenStatus()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());

        Assert.True(ticket.Id > 0);
        Assert.Equal("Open", ticket.Status);
        Assert.Equal("Cannot connect to VPN", ticket.Title);
        Assert.Null(ticket.UpdatedAtUtc);

        var stored = await service.GetByIdAsync(ticket.Id);
        Assert.NotNull(stored);
        Assert.Equal("Jordan Lee", stored.Requester);
        Assert.Equal("VPN is unavailable from home.", stored.Description);

        var history = await service.GetAuditHistoryAsync(ticket.Id);
        var createdEvent = Assert.Single(history);
        Assert.Equal("TicketCreated", createdEvent.EventType);
        Assert.Equal("Ticket created.", createdEvent.Description);
        Assert.Equal("agent-1", createdEvent.ActorId);
        Assert.Equal(
            TimeSpan.FromHours(24),
            ticket.SlaDueAtUtc!.Value - ticket.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidTicketWithoutSaving()
    {
        var ticket = CreateValidTicket();
        ticket.Title = " ";

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(ticket));
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEditableFieldsAndTimestamp()
    {
        var created = await service.CreateAsync(CreateValidTicket());
        var update = CreateValidTicket();
        update.Title = "VPN disconnects";
        update.Status = "In Progress";
        update.AssignedTechnician = "Alex Chen";

        var saved = await service.UpdateAsync(created.Id, update);

        Assert.Equal("VPN disconnects", saved.Title);
        Assert.Equal("Alex Chen", saved.AssignedTechnician);
        Assert.Equal("In Progress", saved.Status);
        Assert.NotNull(saved.UpdatedAtUtc);
        Assert.Equal(created.CreatedAtUtc, saved.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_RecalculatesSlaDeadlineWhenPriorityChanges()
    {
        var created = await service.CreateAsync(CreateValidTicket());
        var update = CreateValidTicket();
        update.Priority = "Critical";

        var saved = await service.UpdateAsync(created.Id, update);

        Assert.Equal(
            TimeSpan.FromHours(4),
            saved.SlaDueAtUtc!.Value - saved.UpdatedAtUtc!.Value);
        Assert.Contains(
            await service.GetAuditHistoryAsync(created.Id),
            item => item.EventType == "SlaChanged");
    }

    [Fact]
    public async Task UpdateStatusAsync_StoresResolutionTimestampAndSlaResult()
    {
        var created = await service.CreateAsync(CreateValidTicket());

        await service.UpdateStatusAsync(created.Id, "Resolved");

        var resolved = await service.GetByIdAsync(created.Id);
        Assert.Equal("Resolved", resolved!.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);
        Assert.True(resolved.SlaMet);

        await service.UpdateStatusAsync(created.Id, "Open");
        var reopened = await service.GetByIdAsync(created.Id);
        Assert.Null(reopened!.ResolvedAtUtc);
        Assert.Null(reopened.SlaMet);
    }

    [Fact]
    public async Task UpdateStatusAsync_RecordsBreachedSlaWhenResolvedAfterDeadline()
    {
        var created = await service.CreateAsync(CreateValidTicket());
        await using (var db = await factory.CreateDbContextAsync())
        {
            var stored = await db.SupportTickets.FindAsync(created.Id);
            stored!.SlaDueAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        await service.UpdateStatusAsync(created.Id, "Resolved");

        var resolved = await service.GetByIdAsync(created.Id);
        Assert.False(resolved!.SlaMet);
        Assert.Equal(SlaState.Breached, slaCalculator.Assess(
            resolved,
            DateTime.UtcNow).State);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidStatusTransition()
    {
        var created = await service.CreateAsync(CreateValidTicket());
        await service.UpdateStatusAsync(created.Id, "Resolved");
        var update = CreateValidTicket();
        update.Status = "In Progress";

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(created.Id, update));
        Assert.Equal("Resolved", (await service.GetByIdAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_AllowsReopeningResolvedTicket()
    {
        var created = await service.CreateAsync(CreateValidTicket());
        await service.UpdateStatusAsync(created.Id, "Resolved");

        await service.UpdateStatusAsync(created.Id, "Open");

        var reopened = await service.GetByIdAsync(created.Id);
        Assert.Equal("Open", reopened!.Status);
        Assert.NotNull(reopened.UpdatedAtUtc);
    }

    [Fact]
    public async Task ArchiveAsync_RetainsTicketCommentsAndAuditHistory()
    {
        var created = await service.CreateAsync(CreateValidTicket());
        await service.AddCommentAsync(created.Id, "Preserve this discussion.");
        var adminService = CreateService("Admin", "admin-1", "admin@example.com");
        var agentService = CreateService("Agent", "agent-2", "agent2@example.com");

        await adminService.ArchiveAsync(created.Id);

        Assert.True((await adminService.GetByIdAsync(created.Id))!.IsArchived);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => agentService.GetByIdAsync(created.Id));
        Assert.Empty(await adminService.GetAllAsync());
        Assert.Equal(created.Id,
            Assert.Single(await adminService.GetAllAsync(includeArchived: true)).Id);
        Assert.Single(await adminService.GetCommentsAsync(created.Id));
        Assert.Equal(
            ["TicketCreated", "CommentAdded", "TicketArchived"],
            (await adminService.GetAuditHistoryAsync(created.Id))
                .Select(item => item.EventType));
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsWhenTicketDoesNotExist()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateStatusAsync(42, "Resolved"));
    }

    [Fact]
    public async Task AddCommentAsync_PersistsTrimmedCommentAndKeepsAuditSeparate()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());

        var comment = await service.AddCommentAsync(ticket.Id, "  Reproduced on Wi-Fi.  ");

        Assert.Equal(ticket.Id, comment.TicketId);
        Assert.Equal("Reproduced on Wi-Fi.", comment.Content);
        Assert.Equal("agent-1", comment.ActorId);
        Assert.True(comment.CreatedAtUtc > DateTime.MinValue);
        Assert.Single(await service.GetCommentsAsync(ticket.Id));
        Assert.Equal(
            ["TicketCreated", "CommentAdded"],
            (await service.GetAuditHistoryAsync(ticket.Id))
                .Select(item => item.EventType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \t ")]
    public async Task AddCommentAsync_RejectsEmptyContentWithoutSaving(string content)
    {
        var ticket = await service.CreateAsync(CreateValidTicket());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddCommentAsync(ticket.Id, content));

        Assert.Empty(await service.GetCommentsAsync(ticket.Id));
    }

    [Fact]
    public async Task AddCommentAsync_RejectsContentLongerThan2000Characters()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddCommentAsync(ticket.Id, new string('x', 2001)));

        Assert.Empty(await service.GetCommentsAsync(ticket.Id));
    }

    [Fact]
    public async Task AddCommentAsync_ThrowsWhenTicketDoesNotExist()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.AddCommentAsync(42, "A valid comment."));
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsCommentsChronologically()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());
        await service.AddCommentAsync(ticket.Id, "First comment");
        await service.AddCommentAsync(ticket.Id, "Second comment");

        var comments = await service.GetCommentsAsync(ticket.Id);

        Assert.Equal(["First comment", "Second comment"],
            comments.Select(comment => comment.Content));
        Assert.True(comments[0].CreatedAtUtc <= comments[1].CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_AuditsStatusPriorityAssignmentAndResolutionChanges()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());
        var update = CreateValidTicket();
        update.Status = "In Progress";
        update.Priority = "Critical";
        update.AssignedTechnician = "Alex Chen";
        update.Description = "VPN fails after sign-in.";
        await service.UpdateAsync(ticket.Id, update);

        update.Status = "Resolved";
        update.ResolutionNotes = "Replaced the VPN profile.";
        await service.UpdateAsync(ticket.Id, update);

        var history = await service.GetAuditHistoryAsync(ticket.Id);
        Assert.Collection(history,
            item => Assert.Equal("TicketCreated", item.EventType),
            item =>
            {
                Assert.Equal("StatusChanged", item.EventType);
                Assert.Contains("Open to In Progress", item.Description);
            },
            item => Assert.Equal("PriorityChanged", item.EventType),
            item => Assert.Equal("SlaChanged", item.EventType),
            item =>
            {
                Assert.Equal("AssignmentChanged", item.EventType);
                Assert.Contains("Unassigned to Alex Chen", item.Description);
            },
            item => Assert.Equal("TicketUpdated", item.EventType),
            item =>
            {
                Assert.Equal("StatusChanged", item.EventType);
                Assert.Contains("In Progress to Resolved", item.Description);
            },
            item => Assert.Equal("ResolutionChanged", item.EventType),
            item => Assert.Equal("ResolutionChanged", item.EventType));
        Assert.All(history, item => Assert.Equal("agent-1", item.ActorId));
        Assert.Equal(history.OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.Id),
            history);
        Assert.DoesNotContain(history, item =>
            item.Description.Contains("Replaced the VPN profile.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedStatusTransition_DoesNotAppendAuditEvents()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());
        await service.UpdateStatusAsync(ticket.Id, "Resolved");
        var historyBefore = await service.GetAuditHistoryAsync(ticket.Id);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateStatusAsync(ticket.Id, "In Progress"));

        var historyAfter = await service.GetAuditHistoryAsync(ticket.Id);
        Assert.Equal(historyBefore.Select(item => item.Id), historyAfter.Select(item => item.Id));
        Assert.Equal("Resolved", (await service.GetByIdAsync(ticket.Id))!.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithNoChanges_DoesNotAppendAuditEvents()
    {
        var ticket = await service.CreateAsync(CreateValidTicket());

        await service.UpdateAsync(ticket.Id, CreateValidTicket());

        Assert.Single(await service.GetAuditHistoryAsync(ticket.Id));
    }

    [Fact]
    public async Task Requester_CanOnlyViewAndCommentOnOwnedTickets()
    {
        var firstRequester = CreateService("Requester", "requester-1", "first@example.com");
        var secondRequester = CreateService("Requester", "requester-2", "second@example.com");
        var ticket = await firstRequester.CreateAsync(new SupportTicket
        {
            Title = "My laptop is offline",
            Description = "Cannot connect to the office network.",
            Category = "Network",
            Requester = "Untrusted submitted requester",
            Priority = "Medium",
            Status = "Resolved"
        });

        Assert.Equal("requester-1", ticket.OwnerUserId);
        Assert.Equal("first@example.com", ticket.Requester);
        Assert.Equal("Open", ticket.Status);
        Assert.Single(await firstRequester.GetAllAsync());
        Assert.Empty(await secondRequester.GetAllAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => secondRequester.GetByIdAsync(ticket.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => secondRequester.AddCommentAsync(ticket.Id, "Not allowed."));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => firstRequester.UpdateStatusAsync(ticket.Id, "Resolved"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => firstRequester.ArchiveAsync(ticket.Id));
    }

    [Fact]
    public async Task AnonymousUsers_CannotReadOrCreateTickets()
    {
        var anonymousService = CreateService(null, null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => anonymousService.GetAllAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => anonymousService.CreateAsync(CreateValidTicket()));
    }

    [Fact]
    public async Task OnlyAdminsCanViewArchivedTickets()
    {
        var requester = CreateService("Requester", "requester-1", "first@example.com");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => requester.GetAllAsync(includeArchived: true));
    }

    [Fact]
    public async Task ReportTickets_UsesRequesterOwnershipAndDateRange()
    {
        var owner = CreateService("Requester", "requester-1", "first@example.com");
        var other = CreateService("Requester", "requester-2", "second@example.com");
        var owned = await owner.CreateAsync(CreateValidTicket());
        var otherTicket = await other.CreateAsync(CreateValidTicket());
        var from = owned.CreatedAtUtc.AddSeconds(-1);
        var until = owned.CreatedAtUtc.AddSeconds(1);

        var reportTickets = await owner.GetReportTicketsAsync(from, until);

        Assert.Equal([owned.Id], reportTickets.Select(ticket => ticket.Id));
        Assert.DoesNotContain(otherTicket.Id, reportTickets.Select(ticket => ticket.Id));
    }

    [Fact]
    public async Task ReportQueries_RejectAnonymousAccessAndInvalidDateRange()
    {
        var anonymous = CreateService(null, null, null);
        var date = DateTime.UtcNow;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => anonymous.GetReportAsync());
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetReportAsync(date, date));
    }

    [Fact]
    public async Task AuditRecordsUseAuthenticatedActorNotSubmittedData()
    {
        var ticket = CreateValidTicket();
        ticket.OwnerUserId = "attacker-controlled";
        var created = await service.CreateAsync(ticket);

        var audit = Assert.Single(await service.GetAuditHistoryAsync(created.Id));
        Assert.Equal("agent-1", audit.ActorId);
        Assert.Equal("agent-1", created.OwnerUserId);
    }

    private static SupportTicket CreateValidTicket() => new()
    {
        Title = "Cannot connect to VPN",
        Description = "VPN is unavailable from home.",
        Category = "Network",
        Requester = "Jordan Lee",
        AssignedTechnician = "",
        Priority = "High",
        Status = "Open"
    };

    private TicketService CreateService(
        string? role,
        string? userId,
        string? userName) =>
        new(factory, new CurrentUserContext(
            new TestAuthenticationStateProvider(CreatePrincipal(role, userId, userName))),
            slaCalculator);

    private static SlaCalculator CreateSlaCalculator() =>
        new(new SlaOptions
        {
            ResolutionTargetHours = new Dictionary<string, double>
            {
                ["Low"] = 120,
                ["Medium"] = 72,
                ["High"] = 24,
                ["Critical"] = 4
            },
            AtRiskWindowHours = 2
        });

    private static ClaimsPrincipal CreatePrincipal(
        string? role,
        string? userId,
        string? userName)
    {
        if (role is null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId!),
            new(ClaimTypes.Name, userName!)
        };
        claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<OpsHubDbContext> options)
        : IDbContextFactory<OpsHubDbContext>
    {
        public OpsHubDbContext CreateDbContext() => new(options);

        public Task<OpsHubDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
