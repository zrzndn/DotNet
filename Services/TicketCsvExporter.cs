using System.Globalization;
using System.Text;
using OpsHub.Models;

namespace OpsHub.Services;

public sealed class TicketCsvExporter
{
    private static readonly string[] Headers =
    [
        "Ticket ID",
        "Title",
        "Category",
        "Priority",
        "Status",
        "Assigned Technician",
        "Created At UTC",
        "Due At UTC",
        "SLA Due At UTC",
        "Resolved At UTC"
    ];

    public byte[] Export(IEnumerable<SupportTicket> tickets)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        var builder = new StringBuilder();
        AppendRow(builder, Headers);
        foreach (var ticket in tickets)
        {
            AppendRow(builder,
            [
                ticket.Id.ToString(CultureInfo.InvariantCulture),
                ticket.Title,
                ticket.Category,
                ticket.Priority,
                ticket.Status,
                ticket.AssignedTechnician,
                FormatUtc(ticket.CreatedAtUtc),
                FormatUtc(ticket.DueAtUtc),
                FormatUtc(ticket.SlaDueAtUtc),
                FormatUtc(ticket.ResolvedAtUtc)
            ]);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(builder.ToString());
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string?> fields)
    {
        builder.AppendJoin(',', fields.Select(FormatField)).Append("\r\n");
    }

    private static string FormatField(string? value)
    {
        var field = value ?? string.Empty;
        var index = 0;
        while (index < field.Length &&
               (char.IsWhiteSpace(field[index]) || char.IsControl(field[index])))
            index++;
        if (index < field.Length && field[index] is '=' or '+' or '-' or '@')
            field = $"'{field}";

        return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string? FormatUtc(DateTime? value) =>
        value is null ? null : FormatUtc(value.Value);

    private static string FormatUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("O", CultureInfo.InvariantCulture);
}
