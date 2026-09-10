using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Regression coverage for <c>CreditCardStatementRepository.GetLatestForCardsAsync</c> — the batched
/// counterpart to <c>GetLatestForCardAsync</c> added to fix the N+1 query pattern that used to sit in
/// <c>CreditCardsListViewModel</c>/<c>SnowballPlanViewModel</c>'s per-card loops (Phase 2 checkpoint
/// review, finding 10). Runs against a real SQLite-backed <see cref="TrackTraceMoneyDbContext"/> so the
/// grouped "latest per card" query is actually exercised, not an in-memory fake.
/// </summary>
public sealed class CreditCardStatementRepositoryLatestForCardsTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public CreditCardStatementRepositoryLatestForCardsTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure($"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetLatestForCardsAsync_ReturnsTheStatementWithTheLatestCycleEndDate_PerCard()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ICreditCardStatementRepository>();

        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();

        context.Set<CreditCardStatement>().Add(new CreditCardStatement(cardA, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 50m, 400m));
        context.Set<CreditCardStatement>().Add(new CreditCardStatement(cardA, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 60m, 500m));
        context.Set<CreditCardStatement>().Add(new CreditCardStatement(cardB, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15), 20m, 100m));
        await context.SaveChangesAsync();

        var result = await repository.GetLatestForCardsAsync([cardA, cardB]);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result[cardA].CycleEndDate);
        Assert.Equal(60m, result[cardA].MinimumPayment);
        Assert.Equal(new DateOnly(2026, 8, 15), result[cardB].CycleEndDate);
    }

    [Fact]
    public async Task GetLatestForCardsAsync_OmitsCardsWithNoStatementAtAll()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ICreditCardStatementRepository>();

        var cardWithStatement = Guid.NewGuid();
        var cardWithoutStatement = Guid.NewGuid();

        context.Set<CreditCardStatement>().Add(new CreditCardStatement(cardWithStatement, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 60m, 500m));
        await context.SaveChangesAsync();

        var result = await repository.GetLatestForCardsAsync([cardWithStatement, cardWithoutStatement]);

        var pair = Assert.Single(result);
        Assert.Equal(cardWithStatement, pair.Key);
        Assert.False(result.ContainsKey(cardWithoutStatement));
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
