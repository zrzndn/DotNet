using System.Globalization;
using OpsHub.Models;

namespace OpsHub.Services;

public enum AnalyticsGrouping
{
    Day,
    Week,
    Month
}

public sealed record AnalyticsTrendPoint(string Label, int Created, int Resolved);

public sealed record AgentWorkloadMetric(
    string Name,
    int OpenTickets,
    int OverdueTickets,
    int ResolvedInPeriod,
    TimeSpan? AverageResolution);

public static class OperationsAnalyticsCalculator
{
    public static IReadOnlyList<AnalyticsTrendPoint> BuildTrendPoints(
        IEnumerable<SupportTicket> tickets,
        DateOnly start,
        DateOnly end,
        AnalyticsGrouping grouping)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ValidateRange(start, end);

        var items = tickets.ToList();
        var rangeStartUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndUtcExclusive = GetEndExclusiveUtc(end);
        var bucketStartUtc = GetBucketStart(rangeStartUtc, grouping);
        var points = new List<AnalyticsTrendPoint>();

        while (bucketStartUtc < rangeEndUtcExclusive)
        {
            var bucketEndUtc = GetBucketEnd(bucketStartUtc, grouping);
            var periodStartUtc = bucketStartUtc < rangeStartUtc
                ? rangeStartUtc
                : bucketStartUtc;
            var periodEndUtc = bucketEndUtc > rangeEndUtcExclusive
                ? rangeEndUtcExclusive
                : bucketEndUtc;

            var created = items.Count(ticket =>
                ticket.CreatedAtUtc >= periodStartUtc &&
                ticket.CreatedAtUtc < periodEndUtc);
            var resolved = items.Count(ticket =>
                ticket.ResolvedAtUtc is DateTime resolvedAtUtc &&
                resolvedAtUtc >= periodStartUtc &&
                resolvedAtUtc < periodEndUtc);

            points.Add(new AnalyticsTrendPoint(
                FormatBucketLabel(bucketStartUtc, bucketEndUtc, grouping),
                created,
                resolved));
            bucketStartUtc = bucketEndUtc;
        }

        return points;
    }

    public static IReadOnlyList<AgentWorkloadMetric> CalculateAgentWorkload(
        IEnumerable<SupportTicket> tickets,
        DateOnly start,
        DateOnly end,
        DateTime nowUtc,
        SlaCalculator slaCalculator)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(slaCalculator);
        ValidateRange(start, end);

        var rangeStartUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndUtcExclusive = GetEndExclusiveUtc(end);
        var normalizedNowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var items = tickets.ToList();

        return items
            .GroupBy(GetAgentName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var assignedTickets = group.ToList();
                var resolvedInPeriod = assignedTickets
                    .Where(ticket =>
                        ticket.Status == "Resolved" &&
                        ticket.ResolvedAtUtc is DateTime resolvedAtUtc &&
                        resolvedAtUtc >= rangeStartUtc &&
                        resolvedAtUtc < rangeEndUtcExclusive)
                    .ToList();
                var resolutionDurations = resolvedInPeriod
                    .Where(ticket => ticket.ResolvedAtUtc >= ticket.CreatedAtUtc)
                    .Select(ticket => ticket.ResolvedAtUtc!.Value -
                                      DateTime.SpecifyKind(ticket.CreatedAtUtc, DateTimeKind.Utc))
                    .ToList();

                return new AgentWorkloadMetric(
                    group.Key,
                    assignedTickets.Count(ticket => ticket.Status != "Resolved"),
                    assignedTickets.Count(ticket =>
                        ticket.Status != "Resolved" &&
                        slaCalculator.Assess(ticket, normalizedNowUtc).State == SlaState.Breached),
                    resolvedInPeriod.Count,
                    resolutionDurations.Count == 0
                        ? null
                        : TimeSpan.FromTicks((long)resolutionDurations.Average(duration => duration.Ticks)));
            })
            .OrderByDescending(row => row.OpenTickets)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetAgentName(SupportTicket ticket) =>
        string.IsNullOrWhiteSpace(ticket.AssignedTechnician)
            ? "Unassigned"
            : ticket.AssignedTechnician;

    private static void ValidateRange(DateOnly start, DateOnly end)
    {
        if (start > end)
            throw new ArgumentException("The reporting start date must be on or before its end date.");
    }

    private static DateTime GetEndExclusiveUtc(DateOnly end) =>
        end == DateOnly.MaxValue
            ? DateTime.MaxValue
            : end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static DateTime GetBucketStart(DateTime rangeStartUtc, AnalyticsGrouping grouping) =>
        grouping switch
        {
            AnalyticsGrouping.Week => rangeStartUtc.AddDays(
                -(((int)rangeStartUtc.DayOfWeek + 6) % 7)),
            AnalyticsGrouping.Month => new DateTime(
                rangeStartUtc.Year,
                rangeStartUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc),
            _ => rangeStartUtc
        };

    private static DateTime GetBucketEnd(DateTime bucketStartUtc, AnalyticsGrouping grouping) =>
        grouping switch
        {
            AnalyticsGrouping.Week => bucketStartUtc.AddDays(7),
            AnalyticsGrouping.Month => bucketStartUtc.AddMonths(1),
            _ => bucketStartUtc.AddDays(1)
        };

    private static string FormatBucketLabel(
        DateTime bucketStartUtc,
        DateTime bucketEndUtc,
        AnalyticsGrouping grouping) =>
        grouping switch
        {
            AnalyticsGrouping.Week => string.Create(
                CultureInfo.InvariantCulture,
                $"{bucketStartUtc:MMM d}–{bucketEndUtc.AddDays(-1):MMM d}"),
            AnalyticsGrouping.Month => bucketStartUtc.ToString("MMM yyyy", CultureInfo.InvariantCulture),
            _ => bucketStartUtc.ToString("MMM d", CultureInfo.InvariantCulture)
        };
}
