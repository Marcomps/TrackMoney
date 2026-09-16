using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Infrastructure.Seeding;

namespace TrackTraceMoney.Infrastructure.Persistence;

/// <summary>
/// See <see cref="IFinanceDatabaseInitializer"/>. A thin wrapper over the same two calls
/// <c>MauiProgram.cs</c>'s startup block already made before the local multi-profile feature existed —
/// migrating <see cref="TrackTraceMoneyDbContext"/> to the latest schema, then seeding the README §12
/// default categories via <see cref="CategorySeeder"/> — extracted so a second call site
/// (first-profile creation; see <c>CreateFirstProfileViewModel</c> in the App project) doesn't have to
/// duplicate the sequence or reference these Infrastructure types directly.
/// </summary>
public sealed class FinanceDatabaseInitializer : IFinanceDatabaseInitializer
{
    private readonly TrackTraceMoneyDbContext _dbContext;

    public FinanceDatabaseInitializer(TrackTraceMoneyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureReadyAsync(CancellationToken ct = default)
    {
        await _dbContext.Database.MigrateAsync(ct).ConfigureAwait(false);
        await CategorySeeder.SeedDefaultCategoriesAsync(_dbContext, ct).ConfigureAwait(false);
    }
}
