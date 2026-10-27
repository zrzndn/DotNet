namespace OpsHub.Services;

public sealed class SlaOptions
{
    public Dictionary<string, double> ResolutionTargetHours { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public double AtRiskWindowHours { get; set; } = 2;
}
