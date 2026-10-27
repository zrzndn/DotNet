using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsHub.Services;

namespace OpsHub.Controllers;

[Authorize(Roles = "Admin,Agent,Requester")]
[Route("reports")]
public sealed class TicketExportController(
    TicketService ticketService,
    TicketCsvExporter exporter) : Controller
{
    [HttpGet("tickets.csv")]
    public async Task<IActionResult> ExportTickets(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        if (from is not null && to is not null && from > to)
            return BadRequest("The start date must be on or before the end date.");

        var fromUtc = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? throughUtcExclusive = to is null
            ? null
            : to.Value == DateOnly.MaxValue
                ? DateTime.MaxValue
                : to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var tickets = await ticketService.GetReportTicketsAsync(
            User,
            fromUtc,
            throughUtcExclusive,
            cancellationToken);
        var bytes = exporter.Export(tickets);
        return File(bytes, "text/csv; charset=utf-8", "opshub-tickets.csv");
    }
}
