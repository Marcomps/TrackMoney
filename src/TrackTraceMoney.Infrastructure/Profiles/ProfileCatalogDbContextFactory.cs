using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrackTraceMoney.Infrastructure.Profiles;

/// <summary>
/// Lets `dotnet ef` design-time tooling create migrations for <see cref="ProfileCatalogDbContext"/>
/// without a startup project — same reasoning as
/// <see cref="Persistence.TrackTraceMoneyDbContextFactory"/> (see its doc comment and the
/// <c>ef-core-migration</c> skill): <c>TrackTraceMoney.App</c> targets <c>net10.0-android</c> only,
/// which <c>dotnet-ef</c> cannot launch as a host, so <c>TrackTraceMoney.Infrastructure</c> is used as
/// both <c>--project</c> and <c>--startup-project</c>. The connection string here is design-time only;
/// the real runtime one is wired up by the App layer via <c>AddTrackTraceMoneyProfileCatalog</c>.
///
/// Because this project now contains two <see cref="DbContext"/> types
/// (<see cref="Persistence.TrackTraceMoneyDbContext"/> and this one), every migration command against
/// either one must pass an explicit <c>--context</c> — `dotnet-ef` refuses to guess between them.
/// </summary>
public sealed class ProfileCatalogDbContextFactory : IDesignTimeDbContextFactory<ProfileCatalogDbContext>
{
    public ProfileCatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProfileCatalogDbContext>()
            .UseSqlite("Data Source=design_time_profiles.db");

        return new ProfileCatalogDbContext(optionsBuilder.Options);
    }
}
