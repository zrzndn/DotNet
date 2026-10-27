using OpsHub.Models;

namespace OpsHub.Services;

public enum SlaState
{
    NotTracked,
    OnTrack,
    AtRisk,
    Breached,
    Met
}

public sealed record SlaAssessment(SlaState State, TimeSpan? Remaining);

public sealed class SlaCalculator
{
    private static readonly string[] RequiredPriorities =
        ["Low", "Medium", "High", "Critical"];

    private readonly Dictionary<string, TimeSpan> resolutionTargets;
    private readonly TimeSpan atRiskWindow;

    public SlaCalculator(SlaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (RequiredPriorities.Any(priority =>
                !options.ResolutionTargetHours.ContainsKey(priority)) ||
            options.ResolutionTargetHours.Any(target =>
                string.IsNullOrWhiteSpace(target.Key) ||
                !double.IsFinite(target.Value) ||
                target.Value <= 0 ||
                target.Value > TimeSpan.MaxValue.TotalHours))
            throw new ArgumentException(
                "SLA resolution targets must define positive finite hours for each priority.",
                nameof(options));
        if (!double.IsFinite(options.AtRiskWindowHours) ||
            options.AtRiskWindowHours <= 0 ||
            options.AtRiskWindowHours > TimeSpan.MaxValue.TotalHours)
            throw new ArgumentException(
                "The SLA at-risk window must be a positive finite number of hours.",
                nameof(options));

        resolutionTargets = options.ResolutionTargetHours.ToDictionary(
            target => target.Key,
            target => TimeSpan.FromHours(target.Value),
            StringComparer.OrdinalIgnoreCase);
        atRiskWindow = TimeSpan.FromHours(options.AtRiskWindowHours);
    }

    public DateTime CalculateDeadline(string priority, DateTime createdAtUtc)
    {
        if (!resolutionTargets.TryGetValue(priority, out var target))
            throw new ArgumentException(
                $"No SLA resolution target is configured for priority '{priority}'.",
                nameof(priority));

        return DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc).Add(target);
    }

    public SlaAssessment Assess(
        SupportTicket ticket,
        DateTime nowUtc)
    {
        if (ticket.Status == "Resolved")
        {
            return ticket.SlaMet switch
            {
                true => new SlaAssessment(SlaState.Met, null),
                false => new SlaAssessment(SlaState.Breached, null),
                null => new SlaAssessment(SlaState.NotTracked, null)
            };
        }

        if (ticket.SlaDueAtUtc is not DateTime deadline)
            return new SlaAssessment(SlaState.NotTracked, null);

        var remaining = DateTime.SpecifyKind(deadline, DateTimeKind.Utc) -
                        DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var state = remaining <= TimeSpan.Zero
            ? SlaState.Breached
            : remaining <= atRiskWindow
                ? SlaState.AtRisk
                : SlaState.OnTrack;
        return new SlaAssessment(state, remaining);
    }
}
