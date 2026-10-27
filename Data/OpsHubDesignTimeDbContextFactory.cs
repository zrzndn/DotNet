using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpsHub.Data;

public sealed class OpsHubDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<OpsHubDbContext>
{
    public OpsHubDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "opshub.db");
        var options = new DbContextOptionsBuilder<OpsHubDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new OpsHubDbContext(options);
    }
}
