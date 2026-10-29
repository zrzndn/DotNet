using OpsHub.Models;
using OpsHub.Services;

namespace OpsHub.Tests;

public sealed class OperationsAnalyticsTests
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

    [Fact]
    public void BuildTrendPoints_CountsCreationAndResolutionByTheirOwnEventDates()
    {
        var tickets = new[]
        {
            new SupportTicket
            {
                CreatedAtUtc = Utc(2026, 10, 1, 10),
                ResolvedAtUtc = Utc(2026, 10, 8, 10)
            },
            new SupportTicket
            {
                CreatedAtUtc = Utc(2026, 10, 8, 11),
                ResolvedAtUtc = Utc(2026, 10, 8, 12)
            },
            new SupportTicket
            {
                CreatedAtUtc = Utc(2026, 9, 30, 23),
                ResolvedAtUtc = Utc(2026, 10, 1, 0)
            },
            new SupportTicket
            {
                CreatedAtUtc = Utc(2026, 10, 8, 23),
                ResolvedAtUtc = Utc(2026, 10, 8, 23)
            },
            new SupportTicket
            {
                CreatedAtUtc = Utc(2026, 10, 9, 0),
                ResolvedAtUtc = Utc(2026, 10, 9, 0)
            }
        };

        var points = OperationsAnalyticsCalculator.BuildTrendPoints(
            tickets,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 8),
            AnalyticsGrouping.Day);

        Assert.Equal(8, points.Count);
        Assert.Equal(new AnalyticsTrendPoint("Oct 1", 1, 1), points[0]);
        Assert.Equal(new AnalyticsTrendPoint("Oct 2", 0, 0), points[1]);
        Assert.Equal(new AnalyticsTrendPoint("Oct 8", 2, 3), points[^1]);
    }

    [Fact]
    public void BuildTrendPoints_UsesCalendarWeekAndMonthBucketsWithZeroIntervals()
    {
        var tickets = new[]
        {
            new SupportTicket { CreatedAtUtc = Utc(2026, 10, 7, 8) },
            new SupportTicket { CreatedAtUtc = Utc(2026, 11, 2, 8) }
        };

        var weekly = OperationsAnalyticsCalculator.BuildTrendPoints(
            tickets,
            new DateOnly(2026, 10, 7),
            new DateOnly(2026, 10, 20),
            AnalyticsGrouping.Week);
        var monthly = OperationsAnalyticsCalculator.BuildTrendPoints(
            tickets,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 2),
            AnalyticsGrouping.Month);

        Assert.Equal(3, weekly.Count);
        Assert.Equal(1, weekly[0].Created);
        Assert.Equal(0, weekly[1].Created);
        Assert.Equal(3, monthly.Count);
        Assert.Equal(1, monthly[0].Created);
        Assert.Equal(1, monthly[1].Created);
        Assert.Equal(0, monthly[2].Created);
    }

    [Fact]
    public void CalculateAgentWorkload_UsesResolutionDateAndIncludesHistoricalOnlyAssignees()
    {
        var tickets = new[]
        {
            new SupportTicket
            {
                AssignedTechnician = "Alex",
                Status = "Resolved",
                CreatedAtUtc = Utc(2026, 9, 1, 10),
                ResolvedAtUtc = Utc(2026, 10, 5, 10),
                SlaMet = true
            },
            new SupportTicket
            {
                AssignedTechnician = "Sam",
                Status = "Resolved",
                CreatedAtUtc = Utc(2026, 10, 5, 10),
                ResolvedAtUtc = Utc(2026, 10, 6, 10)
            },
            new SupportTicket
            {
                AssignedTechnician = "Alex",
                Status = "Open",
                CreatedAtUtc = Utc(2026, 9, 6, 10),
                SlaDueAtUtc = Utc(2026, 10, 7, 10)
            },
            new SupportTicket
            {
                AssignedTechnician = " ",
                Status = "In Progress",
                CreatedAtUtc = Utc(2026, 10, 7, 10),
                SlaDueAtUtc = Utc(2026, 10, 8, 10)
            }
        };

        var rows = OperationsAnalyticsCalculator.CalculateAgentWorkload(
            tickets,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 7),
            Utc(2026, 10, 8, 10),
            slaCalculator);

        var alex = Assert.Single(rows, row => row.Name == "Alex");
        Assert.Equal(1, alex.OpenTickets);
        Assert.Equal(1, alex.OverdueTickets);
        Assert.Equal(1, alex.ResolvedInPeriod);
        Assert.Equal(TimeSpan.FromDays(34), alex.AverageResolution);

        var unassigned = Assert.Single(rows, row => row.Name == "Unassigned");
        Assert.Equal(1, unassigned.OpenTickets);
        Assert.Equal(1, unassigned.OverdueTickets);

        var sam = Assert.Single(rows, row => row.Name == "Sam");
        Assert.Equal(1, sam.ResolvedInPeriod);
        Assert.Equal(TimeSpan.FromDays(1), sam.AverageResolution);
    }

    [Fact]
    public void AnalyticsCalculators_RejectInvalidDateRange()
    {
        var start = new DateOnly(2026, 10, 2);
        var end = new DateOnly(2026, 10, 1);

        Assert.Throws<ArgumentException>(() =>
            OperationsAnalyticsCalculator.BuildTrendPoints(
                [],
                start,
                end,
                AnalyticsGrouping.Day));
        Assert.Throws<ArgumentException>(() =>
            OperationsAnalyticsCalculator.CalculateAgentWorkload(
                [],
                start,
                end,
                DateTime.UtcNow,
                slaCalculator));
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);
}
