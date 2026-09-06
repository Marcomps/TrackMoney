using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrackTraceMoney.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` design-time tooling create migrations without a startup project — needed
/// because TrackTraceMoney.App targets net10.0-android only, which dotnet-ef cannot launch as a
/// host. See the ef-core-migration skill for the full workflow. The connection string here is
/// design-time only; the real runtime one is wired in TrackTraceMoney.App's DI setup.
/// </summary>
public sealed class TrackTraceMoneyDbContextFactory : IDesignTimeDbContextFactory<TrackTraceMoneyDbContext>
{
    public TrackTraceMoneyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TrackTraceMoneyDbContext>()
            .UseSqlite("Data Source=design_time.db");

        return new TrackTraceMoneyDbContext(optionsBuilder.Options);
    }
}
