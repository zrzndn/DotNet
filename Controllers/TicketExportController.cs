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
        string? priority = null,
        string? status = null,
        string? category = null,
        string? assigned = null,
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

        if (!string.IsNullOrWhiteSpace(priority))
            tickets = tickets.Where(ticket => string.Equals(ticket.Priority, priority, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(status))
            tickets = tickets.Where(ticket => string.Equals(ticket.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(category))
            tickets = tickets.Where(ticket => string.Equals(ticket.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(assigned))
        {
            tickets = tickets.Where(ticket =>
                string.Equals(ticket.AssignedTechnician ?? string.Empty, assigned, StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(ticket.AssignedTechnician) && string.Equals("Unassigned", assigned, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var bytes = exporter.Export(tickets);
        return File(bytes, "text/csv; charset=utf-8", "opshub-tickets.csv");
    }
}
