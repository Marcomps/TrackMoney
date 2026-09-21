using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <c>BudgetRepository.HasAnyReferencingCategoryAsync</c> (Category
/// lifecycle slice §A.3).
/// </summary>
public sealed class BudgetRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public BudgetRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task HasAnyReferencingCategoryAsync_MatchesCategoryId()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBudgetRepository>();

        var categoryId = Guid.NewGuid();
        var budget = new Budget(categoryId, 300m, 2026, 9, CurrencyCode.USD);

        await repository.AddAsync(budget);
        await repository.SaveChangesAsync();

        Assert.True(await repository.HasAnyReferencingCategoryAsync(categoryId));
        Assert.False(await repository.HasAnyReferencingCategoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasAnyReferencingCategoryAsync_NoBudgets_ReturnsFalse()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBudgetRepository>();

        Assert.False(await repository.HasAnyReferencingCategoryAsync(Guid.NewGuid()));
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
