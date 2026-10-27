
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using OpsHub.Models;

namespace OpsHub.Data;

public class OpsHubDbContext : IdentityDbContext<ApplicationUser>
{
    public OpsHubDbContext(
        DbContextOptions<OpsHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<SupportTicket> SupportTickets =>
        Set<SupportTicket>();

    public DbSet<TicketComment> TicketComments =>
        Set<TicketComment>();

    public DbSet<TicketAuditEvent> TicketAuditEvents =>
        Set<TicketAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TicketComment>()
            .HasOne(comment => comment.Ticket)
            .WithMany(ticket => ticket.Comments)
            .HasForeignKey(comment => comment.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketComment>()
            .HasIndex(comment => new { comment.TicketId, comment.CreatedAtUtc });

        modelBuilder.Entity<TicketAuditEvent>()
            .HasOne(auditEvent => auditEvent.Ticket)
            .WithMany(ticket => ticket.AuditEvents)
            .HasForeignKey(auditEvent => auditEvent.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketAuditEvent>()
            .HasIndex(auditEvent => new { auditEvent.TicketId, auditEvent.OccurredAtUtc });
    }
}
