using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Persistence;

/// <summary>
/// Verifies the unique index on (CreditAccountId, CycleEndDate) configured in
/// <c>CreditCardStatementConfiguration</c> — a statement cycle's end date must be unique per card. Uses
/// a throwaway file-backed SQLite database per the ef-core-migration skill's guidance (a bare
/// "Data Source=:memory:" connection would need to stay open across `EnsureCreated`/queries, which is
/// more setup than a disposable temp file needs here).
/// </summary>
public sealed class CreditCardStatementConfigurationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");

    private TrackTraceMoneyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TrackTraceMoneyDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        return new TrackTraceMoneyDbContext(options);
    }

    [Fact]
    public async Task SaveChanges_WithDuplicateCycleEndDateForSameCard_ThrowsDueToUniqueIndex()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var creditAccountId = Guid.NewGuid();
        var cycleEndDate = new DateOnly(2026, 9, 25);

        context.CreditCardStatements.Add(new CreditCardStatement(
            creditAccountId,
            new DateOnly(2026, 8, 26),
            cycleEndDate,
            50m,
            200m));
        await context.SaveChangesAsync();

        context.CreditCardStatements.Add(new CreditCardStatement(
            creditAccountId,
            new DateOnly(2026, 8, 26),
            cycleEndDate,
            75m,
            250m));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_WithSameCycleEndDateForDifferentCards_Succeeds()
    {
        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var cycleEndDate = new DateOnly(2026, 9, 25);

        context.CreditCardStatements.Add(new CreditCardStatement(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 26),
            cycleEndDate,
            50m,
            200m));

        context.CreditCardStatements.Add(new CreditCardStatement(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 26),
            cycleEndDate,
            75m,
            250m));

        var exception = await Record.ExceptionAsync(() => context.SaveChangesAsync());
        Assert.Null(exception);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
