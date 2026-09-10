using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Regression coverage for the checkpoint-fix rewrite of <c>TransactionRepository.GetByDateRangeAndCategoryAsync</c>
/// and <c>GetByDateRangeAndSpendAccountAsync</c>: both used to fetch every transaction in the date range
/// via <c>ToListAsync()</c> and filter afterward on the virtual <c>SpendCategoryId</c>/<c>SpendAccountId</c>
/// properties; they now query <see cref="Expense"/> and <see cref="CreditCardPurchase"/> — the only two
/// <see cref="Transaction"/> subtypes that override those properties — separately via <c>OfType&lt;T&gt;()</c>
/// against each type's own mapped column and concatenate the results in memory. These tests run against a
/// real SQLite-backed <see cref="TrackTraceMoneyDbContext"/> (not an in-memory fake) so a query that fails
/// to translate, or a wrong-column mistake, would actually be caught, and confirm the rewritten queries
/// still return the same mixed result set as the old fetch-then-filter implementation did.
/// </summary>
public sealed class TransactionRepositoryDateRangeQueriesTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public TransactionRepositoryDateRangeQueriesTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure($"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetByDateRangeAndCategoryAsync_ReturnsBothExpenseAndCreditCardPurchase_WhenBothInSameCategoryAndRange()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var categoryId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new Expense(new DateOnly(2026, 9, 3), 50m, Guid.NewGuid(), categoryId));
        context.Set<Transaction>().Add(new CreditCardPurchase(new DateOnly(2026, 9, 5), 60m, Guid.NewGuid(), categoryId));
        await context.SaveChangesAsync();

        var results = await repository.GetByDateRangeAndCategoryAsync(from, to, categoryId);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, t => t is Expense && t.Amount == 50m);
        Assert.Contains(results, t => t is CreditCardPurchase && t.Amount == 60m);
    }

    [Fact]
    public async Task GetByDateRangeAndCategoryAsync_ExcludesDifferentCategoryAndOutOfRangeTransactions()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var categoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        // Wrong category, in range.
        context.Set<Transaction>().Add(new Expense(new DateOnly(2026, 9, 3), 50m, Guid.NewGuid(), otherCategoryId));
        // Right category, out of range.
        context.Set<Transaction>().Add(new CreditCardPurchase(new DateOnly(2026, 8, 20), 60m, Guid.NewGuid(), categoryId));
        await context.SaveChangesAsync();

        var results = await repository.GetByDateRangeAndCategoryAsync(from, to, categoryId);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByDateRangeAndSpendAccountAsync_ReturnsBothExpenseAndCreditCardPurchase_WhenBothTargetTheSameSpendAccountIdInRange()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        // Expense.AccountId and CreditCardPurchase.CreditAccountId live in unrelated hierarchies
        // (FinancialAccounts vs. CreditAccounts), but GetByDateRangeAndSpendAccountAsync matches purely
        // on SpendAccountId's value, so reusing one id for both here is a valid way to exercise "both
        // contributing types matched the same requested id" in a single query.
        var spendAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new Expense(new DateOnly(2026, 9, 3), 50m, spendAccountId, categoryId));
        context.Set<Transaction>().Add(new CreditCardPurchase(new DateOnly(2026, 9, 5), 60m, spendAccountId, categoryId));
        await context.SaveChangesAsync();

        var results = await repository.GetByDateRangeAndSpendAccountAsync(from, to, spendAccountId);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, t => t is Expense && t.Amount == 50m);
        Assert.Contains(results, t => t is CreditCardPurchase && t.Amount == 60m);
    }

    [Fact]
    public async Task GetByDateRangeAndSpendAccountAsync_ExcludesNonMatchingAccountAndCreditCardPayment()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var spendAccountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new Expense(new DateOnly(2026, 9, 3), 50m, otherAccountId, categoryId));
        // CreditCardPayment never overrides SpendAccountId (README §17 purchased-vs-paid) — must never surface here.
        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 4), 999m, otherAccountId, spendAccountId));
        await context.SaveChangesAsync();

        var results = await repository.GetByDateRangeAndSpendAccountAsync(from, to, spendAccountId);

        Assert.Empty(results);
    }

    /// <summary>
    /// Regression coverage for the N+1 fix in <c>CreditCardsListViewModel.LoadCreditCardsAsync</c> (Phase
    /// 2 checkpoint review, finding 10): <c>GetCreditCardPaymentsUpToDateForCreditAccountsAsync</c> must
    /// return every matching card's payments in one query — including two different cards' payments
    /// together — while excluding payments past the upper date bound and payments for cards outside the
    /// requested id set.
    /// </summary>
    [Fact]
    public async Task GetCreditCardPaymentsUpToDateForCreditAccountsAsync_ReturnsMatchingPaymentsAcrossMultipleCards_ExcludingOutOfRangeAndOtherCards()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var otherCard = Guid.NewGuid();
        var sourceAccountId = Guid.NewGuid();
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 3), 100m, sourceAccountId, cardA));
        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 9), 200m, sourceAccountId, cardB));
        // Past the upper bound — must be excluded.
        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 10), 300m, sourceAccountId, cardA));
        // A card not in the requested id set — must be excluded.
        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 5), 400m, sourceAccountId, otherCard));
        await context.SaveChangesAsync();

        var results = await repository.GetCreditCardPaymentsUpToDateForCreditAccountsAsync(to, [cardA, cardB]);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.CreditAccountId == cardA && p.Amount == 100m);
        Assert.Contains(results, p => p.CreditAccountId == cardB && p.Amount == 200m);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
