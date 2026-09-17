using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Infrastructure.Seeding;

namespace TrackTraceMoney.Infrastructure.Persistence;

/// <summary>
/// See <see cref="IFinanceDatabaseInitializer"/>. A thin wrapper over the same calls
/// <c>MauiProgram.cs</c>'s startup block already made before the local multi-profile feature existed —
/// migrating <see cref="TrackTraceMoneyDbContext"/> to the latest schema, then seeding the README §12
/// default categories via <see cref="CategorySeeder"/>, the README §7 default "Me" person via
/// <see cref="PersonSeeder"/>, and backfilling the new <c>FinancialInstitution</c> list from every
/// not-yet-migrated free-text institution field via <see cref="FinancialInstitutionBackfillService"/> —
/// extracted so a second call site (first-profile creation; see <c>CreateFirstProfileViewModel</c> in the
/// App project) doesn't have to duplicate the sequence or reference these Infrastructure types directly.
/// All three run here, so both call sites (a brand-new profile, and an existing-install's one-time
/// legacy-database migration into a default profile — see <c>MauiProgram.cs</c>'s startup block) stay in
/// sync for free.
/// </summary>
public sealed class FinanceDatabaseInitializer : IFinanceDatabaseInitializer
{
    private readonly TrackTraceMoneyDbContext _dbContext;

    public FinanceDatabaseInitializer(TrackTraceMoneyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureReadyAsync(string defaultPersonName = "Me", CancellationToken ct = default)
    {
        await _dbContext.Database.MigrateAsync(ct).ConfigureAwait(false);
        await CategorySeeder.SeedDefaultCategoriesAsync(_dbContext, ct).ConfigureAwait(false);
        await PersonSeeder.SeedDefaultPeopleAsync(_dbContext, defaultPersonName, ct).ConfigureAwait(false);
        await FinancialInstitutionBackfillService.BackfillInstitutionsAsync(_dbContext, ct).ConfigureAwait(false);
    }
}
