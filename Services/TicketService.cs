using Microsoft.EntityFrameworkCore;
using OpsHub.Data;
using OpsHub.Models;
using System.Security.Claims;

namespace OpsHub.Services;

public sealed class TicketService(
    IDbContextFactory<OpsHubDbContext> dbFactory,
    CurrentUserContext currentUser,
    SlaCalculator slaCalculator)
{
    public async Task<IReadOnlyList<SupportTicket>> GetAllAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        if (includeArchived && !principal.IsInRole("Admin"))
            throw new UnauthorizedAccessException("Only administrators can view archived tickets.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.SupportTickets
            .AsNoTracking()
            .Where(ticket => includeArchived || !ticket.IsArchived);

        if (principal.IsInRole("Requester"))
        {
            var userId = RequireUserId(principal);
            query = query.Where(ticket => ticket.OwnerUserId == userId);
        }

        return await query
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketReport> GetReportAsync(
        DateTime? fromUtc = null,
        DateTime? throughUtcExclusive = null,
        CancellationToken cancellationToken = default)
    {
        var tickets = await GetReportTicketsAsync(
            fromUtc,
            throughUtcExclusive,
            cancellationToken);
        return TicketReportCalculator.Calculate(tickets);
    }

    public async Task<IReadOnlyList<SupportTicket>> GetReportTicketsAsync(
        DateTime? fromUtc = null,
        DateTime? throughUtcExclusive = null,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        return await GetReportTicketsAsync(
            principal,
            fromUtc,
            throughUtcExclusive,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicket>> GetReportTicketsAsync(
        ClaimsPrincipal principal,
        DateTime? fromUtc = null,
        DateTime? throughUtcExclusive = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        RequireRole(principal, "Admin", "Agent", "Requester");

        if (fromUtc is not null && throughUtcExclusive is not null &&
            fromUtc >= throughUtcExclusive)
            throw new ArgumentException("The report start date must be before its end date.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.SupportTickets
            .AsNoTracking()
            .Where(ticket => !ticket.IsArchived);

        if (principal.IsInRole("Requester"))
        {
            var userId = RequireUserId(principal);
            query = query.Where(ticket => ticket.OwnerUserId == userId);
        }

        if (fromUtc is not null)
            query = query.Where(ticket => ticket.CreatedAtUtc >= fromUtc.Value);
        if (throughUtcExclusive is not null)
            query = query.Where(ticket => ticket.CreatedAtUtc < throughUtcExclusive.Value);

        return await query
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportTicket?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.SupportTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

        EnsureTicketAccess(principal, ticket);
        return ticket;
    }

    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await FindTicketAsync(db, ticketId, cancellationToken);
        EnsureTicketAccess(principal, ticket);
        return await db.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAtUtc)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TicketAuditEvent>> GetAuditHistoryAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await FindTicketAsync(db, ticketId, cancellationToken);
        EnsureTicketAccess(principal, ticket);
        return await db.TicketAuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.TicketId == ticketId)
            .OrderBy(auditEvent => auditEvent.OccurredAtUtc)
            .ThenBy(auditEvent => auditEvent.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketComment> AddCommentAsync(
        int ticketId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        if (!TicketRules.IsValidComment(content))
            throw new ArgumentException(
                "A comment must contain 1–2,000 characters.",
                nameof(content));

        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await FindTicketAsync(db, ticketId, cancellationToken);
        EnsureTicketAccess(principal, ticket);
        if (ticket.IsArchived)
            throw new InvalidOperationException("Comments cannot be added to archived tickets.");

        var actorId = RequireUserId(principal);
        var createdAt = DateTime.UtcNow;

        var comment = new TicketComment
        {
            TicketId = ticketId,
            Content = content!.Trim(),
            ActorId = actorId,
            CreatedAtUtc = createdAt
        };
        db.TicketComments.Add(comment);
        db.TicketAuditEvents.Add(new TicketAuditEvent
        {
            TicketId = ticketId,
            EventType = "CommentAdded",
            Description = "A comment was added to the ticket.",
            ActorId = actorId,
            OccurredAtUtc = createdAt
        });
        await db.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<SupportTicket> CreateAsync(
        SupportTicket input,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent", "Requester");
        var ticket = CopyTicketFields(input);
        var actorId = RequireUserId(principal);
        ticket.Status = "Open";
        ticket.Requester = principal.IsInRole("Requester")
            ? principal.Identity?.Name ?? throw new UnauthorizedAccessException("A username is required.")
            : ticket.Requester;
        ticket.OwnerUserId = actorId;
        ValidateTicket(ticket);
        ticket.CreatedAtUtc = DateTime.UtcNow;
        ticket.UpdatedAtUtc = null;
        ticket.SlaDueAtUtc = slaCalculator.CalculateDeadline(
            ticket.Priority,
            ticket.CreatedAtUtc);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.SupportTickets.Add(ticket);
        ticket.AuditEvents.Add(new TicketAuditEvent
        {
            EventType = "TicketCreated",
            Description = "Ticket created.",
            ActorId = actorId,
            OccurredAtUtc = ticket.CreatedAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<SupportTicket> UpdateAsync(
        int id,
        SupportTicket input,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent");
        var ticket = CopyTicketFields(input);
        ValidateTicket(ticket);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket #{id} was not found.");

        ValidateStatusTransition(existing.Status, ticket.Status);
        if (existing.IsArchived)
            throw new InvalidOperationException("Archived tickets cannot be edited.");

        var changedAt = DateTime.UtcNow;
        AddChangeEvents(db, existing, ticket, changedAt, RequireUserId(principal));
        if (existing.Priority != ticket.Priority)
            existing.SlaDueAtUtc = slaCalculator.CalculateDeadline(ticket.Priority, changedAt);
        ApplyResolutionState(existing, ticket.Status, changedAt);
        CopyTicketFields(ticket, existing);
        existing.UpdatedAtUtc = changedAt;

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task UpdateStatusAsync(
        int id,
        string status,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin", "Agent");
        if (!TicketRules.IsValidStatus(status))
            throw new ArgumentException("Choose a valid ticket status.", nameof(status));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket #{id} was not found.");

        if (ticket.IsArchived)
            throw new InvalidOperationException("Archived tickets cannot be changed.");

        ValidateStatusTransition(ticket.Status, status);
        var changedAt = DateTime.UtcNow;
        var updatedTicket = CopyTicketFields(ticket);
        updatedTicket.Status = status;
        AddChangeEvents(db, ticket, updatedTicket, changedAt, RequireUserId(principal));
        ApplyResolutionState(ticket, status, changedAt);
        ticket.Status = status;
        ticket.UpdatedAtUtc = changedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var principal = await RequireRoleAsync("Admin");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket #{id} was not found.");

        if (ticket.IsArchived)
            return;

        var archivedAt = DateTime.UtcNow;
        ticket.IsArchived = true;
        ticket.ArchivedAtUtc = archivedAt;
        ticket.UpdatedAtUtc = archivedAt;
        db.TicketAuditEvents.Add(new TicketAuditEvent
        {
            TicketId = ticket.Id,
            EventType = "TicketArchived",
            Description = "Ticket archived.",
            ActorId = RequireUserId(principal),
            OccurredAtUtc = archivedAt
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateTicket(SupportTicket ticket)
    {
        if (TicketRules.IsValidTicket(ticket))
            return;

        throw new ArgumentException(
            "Check the ticket title, description, requester, category, priority, status, and notes.");
    }

    private static void ValidateStatusTransition(string current, string next)
    {
        if (!TicketRules.IsValidStatusTransition(current, next))
            throw new ArgumentException($"A ticket cannot move from {current} to {next}.");
    }

    private static void AddChangeEvents(
        OpsHubDbContext db,
        SupportTicket current,
        SupportTicket updated,
        DateTime changedAt,
        string actorId)
    {
        void Add(string eventType, string description) =>
            db.TicketAuditEvents.Add(new TicketAuditEvent
            {
                TicketId = current.Id,
                EventType = eventType,
                Description = description,
                ActorId = actorId,
                OccurredAtUtc = changedAt
            });

        if (current.Status != updated.Status)
            Add("StatusChanged", $"Status changed from {current.Status} to {updated.Status}.");

        if (current.Priority != updated.Priority)
        {
            Add("PriorityChanged", $"Priority changed from {current.Priority} to {updated.Priority}.");
            Add("SlaChanged", "The resolution target was recalculated for the new priority.");
        }

        if (current.AssignedTechnician != updated.AssignedTechnician)
        {
            var oldAssignee = string.IsNullOrWhiteSpace(current.AssignedTechnician)
                ? "Unassigned"
                : current.AssignedTechnician;
            var newAssignee = string.IsNullOrWhiteSpace(updated.AssignedTechnician)
                ? "Unassigned"
                : updated.AssignedTechnician;
            Add("AssignmentChanged", $"Assignment changed from {oldAssignee} to {newAssignee}.");
        }

        if (current.ResolutionNotes != updated.ResolutionNotes)
            Add("ResolutionChanged", string.IsNullOrWhiteSpace(updated.ResolutionNotes)
                ? "Resolution notes cleared."
                : "Resolution notes updated.");

        if (current.Status != updated.Status &&
            (current.Status == "Resolved" || updated.Status == "Resolved"))
            Add("ResolutionChanged", updated.Status == "Resolved"
                ? "Ticket marked as resolved."
                : "Ticket reopened.");

        if (current.Title != updated.Title)
            Add("TicketUpdated", "Ticket title updated.");

        if (current.Description != updated.Description)
            Add("TicketUpdated", "Ticket description updated.");

        if (current.Category != updated.Category)
            Add("TicketUpdated", "Ticket category updated.");

        if (current.DueAtUtc != updated.DueAtUtc)
            Add("TicketUpdated", "Ticket due date updated.");
    }

    private void ApplyResolutionState(
        SupportTicket ticket,
        string nextStatus,
        DateTime changedAtUtc)
    {
        if (ticket.Status == nextStatus)
            return;

        if (nextStatus == "Resolved")
        {
            ticket.ResolvedAtUtc = changedAtUtc;
            ticket.SlaMet = ticket.SlaDueAtUtc is DateTime deadline
                ? changedAtUtc <= deadline
                : null;
        }
        else if (ticket.Status == "Resolved")
        {
            ticket.ResolvedAtUtc = null;
            ticket.SlaMet = null;
        }
    }

    private static async Task<SupportTicket> FindTicketAsync(
        OpsHubDbContext db,
        int ticketId,
        CancellationToken cancellationToken)
    {
        return await db.SupportTickets.FirstOrDefaultAsync(
                   ticket => ticket.Id == ticketId,
                   cancellationToken)
               ?? throw new KeyNotFoundException($"Ticket #{ticketId} was not found.");
    }

    private async Task<ClaimsPrincipal> RequireRoleAsync(params string[] roles)
    {
        var principal = await currentUser.GetPrincipalAsync();
        RequireRole(principal, roles);
        return principal;
    }

    private static void RequireRole(ClaimsPrincipal principal, params string[] roles)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !roles.Any(principal.IsInRole))
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");
    }

    private static string RequireUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Your account is missing its identity.");

    private static void EnsureTicketAccess(
        ClaimsPrincipal principal,
        SupportTicket? ticket)
    {
        if (ticket is null)
            throw new KeyNotFoundException("The ticket was not found.");

        if (ticket.IsArchived && !principal.IsInRole("Admin"))
            throw new UnauthorizedAccessException("Only administrators can view archived tickets.");

        if (principal.IsInRole("Admin") || principal.IsInRole("Agent"))
            return;

        if (principal.IsInRole("Requester") &&
            !ticket.IsArchived &&
            ticket.OwnerUserId == RequireUserId(principal))
            return;

        throw new UnauthorizedAccessException("You do not have access to this ticket.");
    }

    private static SupportTicket CopyTicketFields(SupportTicket input) => new()
    {
        Title = input.Title?.Trim() ?? string.Empty,
        Description = input.Description?.Trim() ?? string.Empty,
        Category = input.Category?.Trim() ?? string.Empty,
        Requester = input.Requester?.Trim() ?? string.Empty,
        AssignedTechnician = input.AssignedTechnician?.Trim() ?? string.Empty,
        Priority = input.Priority?.Trim() ?? string.Empty,
        Status = input.Status?.Trim() ?? string.Empty,
        DueAtUtc = input.DueAtUtc,
        ResolutionNotes = input.ResolutionNotes?.Trim() ?? string.Empty
    };

    private static void CopyTicketFields(SupportTicket source, SupportTicket destination)
    {
        destination.Title = source.Title;
        destination.Description = source.Description;
        destination.Category = source.Category;
        destination.Requester = source.Requester;
        destination.AssignedTechnician = source.AssignedTechnician;
        destination.Priority = source.Priority;
        destination.Status = source.Status;
        destination.DueAtUtc = source.DueAtUtc;
        destination.ResolutionNotes = source.ResolutionNotes;
    }
}
