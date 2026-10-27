using System.Text;
using OpsHub.Models;
using OpsHub.Services;

namespace OpsHub.Tests;

public sealed class ReportingTests
{
    private readonly SlaCalculator slaCalculator = new(new SlaOptions
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

    [Theory]
    [InlineData("Low", 120)]
    [InlineData("Medium", 72)]
    [InlineData("High", 24)]
    [InlineData("Critical", 4)]
    public void CalculateDeadline_UsesConfiguredElapsedHours(
        string priority,
        int targetHours)
    {
        var createdAt = new DateTime(2026, 10, 27, 8, 30, 0, DateTimeKind.Utc);

        var deadline = slaCalculator.CalculateDeadline(priority, createdAt);

        Assert.Equal(createdAt.AddHours(targetHours), deadline);
        Assert.Equal(DateTimeKind.Utc, deadline.Kind);
    }

    [Fact]
    public void CalculateDeadline_RejectsUnknownPriority()
    {
        Assert.Throws<ArgumentException>(() =>
            slaCalculator.CalculateDeadline("Urgent", DateTime.UtcNow));
    }

    [Fact]
    public void Assess_DistinguishesOnTrackAtRiskAndBreached()
    {
        var now = new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);

        Assert.Equal(SlaState.OnTrack, slaCalculator.Assess(
            ActiveTicket(now.AddHours(3)), now).State);
        var atRisk = slaCalculator.Assess(ActiveTicket(now.AddHours(1)), now);
        Assert.Equal(SlaState.AtRisk, atRisk.State);
        Assert.Equal(TimeSpan.FromHours(1), atRisk.Remaining);
        Assert.Equal(SlaState.Breached, slaCalculator.Assess(
            ActiveTicket(now.AddSeconds(-1)), now).State);
    }

    [Theory]
    [InlineData(true, SlaState.Met)]
    [InlineData(false, SlaState.Breached)]
    [InlineData(null, SlaState.NotTracked)]
    public void Assess_UsesStoredResolutionOutcome(
        bool? slaMet,
        SlaState expected)
    {
        var ticket = new SupportTicket { Status = "Resolved", SlaMet = slaMet };

        Assert.Equal(expected, slaCalculator.Assess(ticket, DateTime.UtcNow).State);
    }

    [Fact]
    public void ReportCalculator_ComputesMetricsAndBreakdowns()
    {
        var now = DateTime.UtcNow;
        var tickets = new[]
        {
            new SupportTicket
            {
                Priority = "High",
                Category = "Network",
                Status = "Resolved",
                AssignedTechnician = "Alex",
                CreatedAtUtc = now.AddHours(-2),
                ResolvedAtUtc = now.AddHours(-1),
                SlaMet = true
            },
            new SupportTicket
            {
                Priority = "Critical",
                Category = "Hardware",
                Status = "Open",
                AssignedTechnician = "",
                CreatedAtUtc = now
            },
            new SupportTicket
            {
                Priority = "Low",
                Category = "Software",
                Status = "Resolved",
                CreatedAtUtc = now.AddHours(-8)
            }
        };

        var report = TicketReportCalculator.Calculate(tickets);

        Assert.Equal(3, report.TotalTickets);
        Assert.Equal(2, report.ResolvedTickets);
        Assert.Equal(200d / 3, report.ResolutionRatePercent, 5);
        Assert.Equal(TimeSpan.FromHours(1), report.AverageResolutionTime);
        Assert.Equal(1, report.SlaEvaluatedTickets);
        Assert.Equal(100, report.SlaCompliancePercent);
        Assert.Contains(report.ByTechnician,
            row => row.Name == "Unassigned" && row.Count == 2);
        Assert.Equal(3, report.ByCategory.Count);
    }

    [Fact]
    public void ReportCalculator_HandlesEmptyData()
    {
        var report = TicketReportCalculator.Calculate([]);

        Assert.False(report.HasData);
        Assert.Equal(0, report.TotalTickets);
        Assert.Equal(0, report.ResolutionRatePercent);
        Assert.Null(report.AverageResolutionTime);
        Assert.Null(report.SlaCompliancePercent);
    }

    [Fact]
    public void CsvExporter_EscapesFieldsAndNeutralizesSpreadsheetFormulas()
    {
        var exporter = new TicketCsvExporter();
        var csv = Encoding.UTF8.GetString(exporter.Export(
        [
            new SupportTicket
            {
                Id = 17,
                Title = "=HYPERLINK(\"https://example.test\")",
                Category = "Network, WAN",
                AssignedTechnician = "  +cmd",
                Status = "In Progress"
            },
            new SupportTicket
            {
                Id = 18,
                Title = "Line \"one\"\r\nLine two"
            }
        ]));

        Assert.StartsWith(
            "\"Ticket ID\",\"Title\",\"Category\",\"Priority\",\"Status\"",
            csv);
        Assert.Contains("\"'=HYPERLINK(\"\"https://example.test\"\")\"", csv);
        Assert.Contains("\"Network, WAN\"", csv);
        Assert.Contains("\"'  +cmd\"", csv);
        Assert.Contains("\"Line \"\"one\"\"\r\nLine two\"", csv);
        Assert.EndsWith("\r\n", csv);
    }

    private static SupportTicket ActiveTicket(DateTime deadline) => new()
    {
        Status = "Open",
        SlaDueAtUtc = deadline
    };
}
