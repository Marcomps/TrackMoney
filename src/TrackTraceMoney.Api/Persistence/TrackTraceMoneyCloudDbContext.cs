using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Persistence;

/// <summary>
/// Cloud-side EF Core context for <c>TrackTraceMoney.Api</c>, backed by PostgreSQL
/// (<c>Npgsql.EntityFrameworkCore.PostgreSQL</c>). This is a brand-new, independent persistence
/// layer — NOT the on-device SQLite <c>TrackTraceMoneyDbContext</c> from
/// <c>TrackTraceMoney.Infrastructure</c>, which remains the offline-first source of truth on the
/// device (README §2.2) and is untouched by this project.
///
/// Per README §51/§48 and the 2026-09-14 product decision, Phase 4's sync model is
/// backup-only/user-triggered (no live sync, no conflict resolution), so this context is expected
/// to eventually model things like hashed-credential user accounts and opaque cloud backup blobs —
/// NOT a mirrored copy of the full on-device domain model. No such entities exist yet; this slice
/// only proves the PostgreSQL + EF Core migration pipeline boots and applies cleanly.
/// </summary>
public sealed class TrackTraceMoneyCloudDbContext : DbContext
{
    public TrackTraceMoneyCloudDbContext(DbContextOptions<TrackTraceMoneyCloudDbContext> options)
        : base(options)
    {
    }

    public DbSet<CloudUser> Users => Set<CloudUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackTraceMoneyCloudDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
