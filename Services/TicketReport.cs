using OpsHub.Models;

namespace OpsHub.Services;

public sealed record TicketBreakdown(string Name, int Count);

public sealed record TicketReport(
    int TotalTickets,
    int ResolvedTickets,
    double ResolutionRatePercent,
    TimeSpan? AverageResolutionTime,
    int SlaEvaluatedTickets,
    double? SlaCompliancePercent,
    IReadOnlyList<TicketBreakdown> ByPriority,
    IReadOnlyList<TicketBreakdown> ByStatus,
    IReadOnlyList<TicketBreakdown> ByCategory,
    IReadOnlyList<TicketBreakdown> ByTechnician)
{
    public bool HasData => TotalTickets > 0;
}

public static class TicketReportCalculator
{
    public static TicketReport Calculate(IEnumerable<SupportTicket> tickets)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        var items = tickets.ToList();
        var resolvedCount = items.Count(ticket => ticket.Status == "Resolved");
        var resolutionTimes = items
            .Where(ticket => ticket.Status == "Resolved" &&
                             ticket.ResolvedAtUtc is not null)
            .ToList();
        var evaluated = items
            .Where(ticket => ticket.Status == "Resolved" &&
                             ticket.SlaMet is not null)
            .ToList();

        return new TicketReport(
            items.Count,
            resolvedCount,
            items.Count == 0 ? 0 : resolvedCount * 100d / items.Count,
            resolutionTimes.Count == 0
                ? null
                : TimeSpan.FromTicks((long)resolutionTimes.Average(ticket =>
                    (DateTime.SpecifyKind(ticket.ResolvedAtUtc!.Value, DateTimeKind.Utc) -
                     DateTime.SpecifyKind(ticket.CreatedAtUtc, DateTimeKind.Utc)).Ticks)),
            evaluated.Count,
            evaluated.Count == 0
                ? null
                : evaluated.Count(ticket => ticket.SlaMet == true) * 100d / evaluated.Count,
            Group(items, ticket => ticket.Priority),
            Group(items, ticket => ticket.Status),
            Group(items, ticket => ticket.Category),
            Group(items, ticket => string.IsNullOrWhiteSpace(ticket.AssignedTechnician)
                ? "Unassigned"
                : ticket.AssignedTechnician));
    }

    private static IReadOnlyList<TicketBreakdown> Group(
        IEnumerable<SupportTicket> tickets,
        Func<SupportTicket, string> keySelector) =>
        tickets.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TicketBreakdown(group.Key, group.Count()))
            .ToList();
}
