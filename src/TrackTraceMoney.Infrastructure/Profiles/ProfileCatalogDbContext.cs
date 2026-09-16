using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Domain.Profiles;

namespace TrackTraceMoney.Infrastructure.Profiles;

/// <summary>
/// The small, always-open "catalog" database (<c>tracktracemoney_profiles.db3</c>) that lists which
/// local, password-less profiles exist on this device — completely separate from
/// <see cref="Persistence.TrackTraceMoneyDbContext"/>, which is opened per-profile against
/// <c>tracktracemoney_{profileId}.db3</c> and is unchanged by this feature (no <c>ProfileId</c> column
/// anywhere in the finance schema; the per-file split is what provides isolation between profiles).
/// </summary>
/// <remarks>
/// <b>Critical:</b> <see cref="OnModelCreating"/> must apply <see cref="LocalProfileGroupConfiguration"/>
/// and <see cref="LocalProfileConfiguration"/> individually. Do NOT switch this to
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfileCatalogDbContext).Assembly)</c> the way
/// <see cref="Persistence.TrackTraceMoneyDbContext"/> does — that scans the whole
/// <c>TrackTraceMoney.Infrastructure</c> assembly and would silently pull every finance-schema
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> (PersonConfiguration, CategoryConfiguration,
/// TransactionConfiguration, ...) into this small catalog database too.
/// </remarks>
public sealed class ProfileCatalogDbContext : DbContext
{
    public DbSet<LocalProfileGroup> Groups => Set<LocalProfileGroup>();

    public DbSet<LocalProfile> Profiles => Set<LocalProfile>();

    public ProfileCatalogDbContext(DbContextOptions<ProfileCatalogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LocalProfileGroupConfiguration());
        modelBuilder.ApplyConfiguration(new LocalProfileConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
