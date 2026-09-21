using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <c>RecurringExpenseRepository.HasAnyReferencingAccountAsync</c>
/// (edit/delete slice spec §0) — checks both <see cref="RecurringExpense.AccountId"/> (the
/// <c>FinancialAccount</c>-backed case) and <see cref="RecurringExpense.CreditAccountId"/> (the
/// credit-card-backed case), and that deactivated rows still count.
/// </summary>
public sealed class RecurringExpenseRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public RecurringExpenseRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task HasAnyReferencingAccountAsync_MatchesAccountId_IncludingDeactivatedRows()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringExpenseRepository>();

        var accountId = Guid.NewGuid();
        var recurringExpense = new RecurringExpense(
            "Rent", 500m, Guid.NewGuid(), accountId, RecurringExpenseFrequency.Monthly, new DateOnly(2026, 1, 1), null);
        recurringExpense.Deactivate();

        await repository.AddAsync(recurringExpense);
        await repository.SaveChangesAsync();

        Assert.True(await repository.HasAnyReferencingAccountAsync(accountId));
        Assert.False(await repository.HasAnyReferencingAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasAnyReferencingAccountAsync_MatchesCreditAccountId()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringExpenseRepository>();

        var creditAccountId = Guid.NewGuid();
        var recurringExpense = RecurringExpense.ForCreditCard(
            "Netflix", 15m, Guid.NewGuid(), creditAccountId, RecurringExpenseFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        await repository.AddAsync(recurringExpense);
        await repository.SaveChangesAsync();

        Assert.True(await repository.HasAnyReferencingAccountAsync(creditAccountId));
        Assert.False(await repository.HasAnyReferencingAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasAnyReferencingCategoryAsync_MatchesCategoryId_IncludingDeactivatedRows()
    {
        // Category lifecycle slice §A.3: deliberately includes inactive rows -- a deactivated recurring
        // expense's category reference would still dangle if the category were hard-deleted.
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecurringExpenseRepository>();

        var categoryId = Guid.NewGuid();
        var recurringExpense = new RecurringExpense(
            "Rent", 500m, categoryId, Guid.NewGuid(), RecurringExpenseFrequency.Monthly, new DateOnly(2026, 1, 1), null);
        recurringExpense.Deactivate();

        await repository.AddAsync(recurringExpense);
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
