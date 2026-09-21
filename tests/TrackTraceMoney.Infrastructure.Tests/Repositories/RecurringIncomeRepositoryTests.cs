using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.RecurringIncomes;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <see cref="RecurringIncomeRepository"/>, mirroring
/// <c>NetWorthSnapshotRepositoryTests</c>' shape.
/// </summary>
public sealed class RecurringIncomeRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public RecurringIncomeRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFields()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringIncomeRepository>();

        var categoryId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 1, 15);
        var recurringIncome = new RecurringIncome(
            "Salary", 2000m, categoryId, accountId, RecurringIncomeFrequency.Biweekly, startDate, endDate: null);

        await repository.AddAsync(recurringIncome);
        await repository.SaveChangesAsync();

        var reloaded = await repository.GetByIdAsync(recurringIncome.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Salary", reloaded!.Name);
        Assert.Equal(2000m, reloaded.Amount);
        Assert.Equal(categoryId, reloaded.CategoryId);
        Assert.Equal(accountId, reloaded.DestinationAccountId);
        Assert.Equal(RecurringIncomeFrequency.Biweekly, reloaded.Frequency);
        Assert.Equal(startDate, reloaded.StartDate);
        Assert.Null(reloaded.EndDate);
        Assert.Null(reloaded.LastConfirmedDate);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task GetActiveAsync_FiltersOutDeactivatedEntries()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringIncomeRepository>();

        var active = new RecurringIncome(
            "Salary", 2000m, Guid.NewGuid(), Guid.NewGuid(), RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 1), null);
        var inactive = new RecurringIncome(
            "Old side gig", 100m, Guid.NewGuid(), Guid.NewGuid(), RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 1), null);
        inactive.Deactivate();

        await repository.AddAsync(active);
        await repository.AddAsync(inactive);
        await repository.SaveChangesAsync();

        var result = await repository.GetActiveAsync();

        var single = Assert.Single(result);
        Assert.Equal(active.Id, single.Id);
    }

    [Fact]
    public async Task HasAnyReferencingAccountAsync_MatchesDestinationAccountId_IncludingDeactivatedRows()
    {
        // Edit/delete slice spec §0: deliberately includes inactive rows -- a deactivated recurring
        // income's account reference would still dangle if the account were hard-deleted.
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringIncomeRepository>();

        var accountId = Guid.NewGuid();
        var recurringIncome = new RecurringIncome(
            "Salary", 2000m, Guid.NewGuid(), accountId, RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 1), null);
        recurringIncome.Deactivate();

        await repository.AddAsync(recurringIncome);
        await repository.SaveChangesAsync();

        Assert.True(await repository.HasAnyReferencingAccountAsync(accountId));
        Assert.False(await repository.HasAnyReferencingAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasAnyReferencingCategoryAsync_MatchesCategoryId_IncludingDeactivatedRows()
    {
        // Category lifecycle slice §A.3: deliberately includes inactive rows -- a deactivated recurring
        // income's category reference would still dangle if the category were hard-deleted.
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringIncomeRepository>();

        var categoryId = Guid.NewGuid();
        var recurringIncome = new RecurringIncome(
            "Salary", 2000m, categoryId, Guid.NewGuid(), RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 1), null);
        recurringIncome.Deactivate();

        await repository.AddAsync(recurringIncome);
        await repository.SaveChangesAsync();

        Assert.True(await repository.HasAnyReferencingCategoryAsync(categoryId));
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
