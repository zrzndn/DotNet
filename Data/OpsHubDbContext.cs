
using Microsoft.EntityFrameworkCore;
using OpsHub.Models;

namespace OpsHub.Data;

public class OpsHubDbContext : DbContext
{
    public OpsHubDbContext(
        DbContextOptions<OpsHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<SupportTicket> SupportTickets =>
        Set<SupportTicket>();
}
