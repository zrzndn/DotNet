
using OpsHub.Models;

namespace OpsHub.Services;

public static class TicketRules
{
    public static bool IsValidTicket(SupportTicket ticket) =>
        IsValidTitle(ticket.Title) &&
        IsValidDescription(ticket.Description) &&
        IsValidRequester(ticket.Requester) &&
        IsValidTechnician(ticket.AssignedTechnician) &&
        IsValidCategory(ticket.Category) &&
        IsValidPriority(ticket.Priority) &&
        IsValidStatus(ticket.Status) &&
        IsValidResolutionNotes(ticket.ResolutionNotes);

    public static bool IsValidTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        title.Trim().Length <= 200;

    public static bool IsValidDescription(string? description) =>
        !string.IsNullOrWhiteSpace(description) &&
        description.Trim().Length <= 2000;

    public static bool IsValidRequester(string? requester) =>
        !string.IsNullOrWhiteSpace(requester) &&
        requester.Trim().Length <= 150;

    public static bool IsValidTechnician(string? technician) =>
        technician is null || technician.Trim().Length <= 150;

    public static bool IsValidResolutionNotes(string? notes) =>
        notes is null || notes.Trim().Length <= 2000;

    public static bool IsValidComment(string? content) =>
        !string.IsNullOrWhiteSpace(content) &&
        content.Trim().Length <= 2000;

    public static bool IsValidCategory(string? category) =>
        category is "Hardware"
            or "Network"
            or "Software"
            or "Access and Accounts";

    public static bool IsValidPriority(string? priority) =>
        priority is "Low"
            or "Medium"
            or "High"
            or "Critical";

    public static bool IsValidStatus(string? status) =>
        status is "Open"
            or "In Progress"
            or "Resolved";

    public static bool IsValidStatusTransition(string? current, string? next) =>
        current == next ||
        (current, next) switch
        {
            ("Open", "In Progress") => true,
            ("Open", "Resolved") => true,
            ("In Progress", "Open") => true,
            ("In Progress", "Resolved") => true,
            ("Resolved", "Open") => true,
            _ => false
        };
}
