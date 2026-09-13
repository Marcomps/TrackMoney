using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.NetWorth;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <see cref="NetWorthSnapshotRepository"/> (README §24) — exercises
/// the actual unique index on (Currency, AsOfDate), which an in-memory fake can't verify.
/// </summary>
public sealed class NetWorthSnapshotRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public NetWorthSnapshotRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure($"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task RoundTrip_SaveAndReload_PreservesAllFields()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INetWorthSnapshotRepository>();

        var snapshot = new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 9, 13), 1500m, 400m);
        await repository.AddAsync(snapshot);
        await repository.SaveChangesAsync();

        var reloaded = await repository.GetByIdAsync(snapshot.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(CurrencyCode.USD, reloaded!.Currency);
        Assert.Equal(new DateOnly(2026, 9, 13), reloaded.AsOfDate);
        Assert.Equal(1500m, reloaded.TotalAssets);
        Assert.Equal(400m, reloaded.TotalLiabilities);
        Assert.Equal(1100m, reloaded.NetWorth);
    }

    [Fact]
    public async Task GetByCurrencyAndDateAsync_FindsExactMatch_AndReturnsNullOtherwise()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INetWorthSnapshotRepository>();

        await repository.AddAsync(new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 9, 13), 1000m, 0m));
        await repository.SaveChangesAsync();

        var found = await repository.GetByCurrencyAndDateAsync(CurrencyCode.USD, new DateOnly(2026, 9, 13));
        var notFoundByDate = await repository.GetByCurrencyAndDateAsync(CurrencyCode.USD, new DateOnly(2026, 9, 14));
        var notFoundByCurrency = await repository.GetByCurrencyAndDateAsync(CurrencyCode.MXN, new DateOnly(2026, 9, 13));

        Assert.NotNull(found);
        Assert.Null(notFoundByDate);
        Assert.Null(notFoundByCurrency);
    }

    [Fact]
    public async Task GetForCurrencyAsync_ReturnsOnlyThatCurrencyRows_InAscendingAsOfDateOrder()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INetWorthSnapshotRepository>();

        await repository.AddAsync(new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 9, 14), 1200m, 0m));
        await repository.AddAsync(new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 9, 12), 1000m, 0m));
        await repository.AddAsync(new NetWorthSnapshot(CurrencyCode.MXN, new DateOnly(2026, 9, 13), 5000m, 0m));
        await repository.SaveChangesAsync();

        var result = await repository.GetForCurrencyAsync(CurrencyCode.USD);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 9, 12), result[0].AsOfDate);
        Assert.Equal(new DateOnly(2026, 9, 14), result[1].AsOfDate);
    }

    [Fact]
    public async Task DuplicateCurrencyAndDateInsert_BypassingTheService_Throws()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INetWorthSnapshotRepository>();

        await repository.AddAsync(new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 9, 13), 1000m, 0m));
        await repository.SaveChangesAsync();

        await repository.AddAsync(new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 9, 13), 2000m, 0m));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
