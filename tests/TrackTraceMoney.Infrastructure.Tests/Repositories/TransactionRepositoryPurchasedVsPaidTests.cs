using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// README §17 "purchased vs. paid": <see cref="CreditCardPayment"/> never overrides
/// <c>SpendAccountId</c>, so <c>GetByDateRangeAndSpendAccountAsync</c> must never surface it, and
/// symmetrically <c>GetCreditCardPaymentsByDateRangeAndCreditAccountAsync</c> must never surface a
/// <see cref="CreditCardPurchase"/> — each side of the comparison must draw from a disjoint set of
/// transaction rows so purchases and payments are never double counted or swapped.
/// </summary>
public sealed class TransactionRepositoryPurchasedVsPaidTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public TransactionRepositoryPurchasedVsPaidTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure($"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetCreditCardPaymentsByDateRangeAndCreditAccountAsync_ReturnsNothing_WhenOnlyPurchasesExistInRange()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var creditAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new CreditCardPurchase(new DateOnly(2026, 9, 5), 150m, creditAccountId, categoryId));
        await context.SaveChangesAsync();

        var payments = await repository.GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(from, to, creditAccountId);

        Assert.Empty(payments);
    }

    [Fact]
    public async Task GetByDateRangeAndSpendAccountAsync_ReturnsNothing_WhenOnlyPaymentsExistInRange()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var creditAccountId = Guid.NewGuid();
        var sourceAccountId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 3), 300m, sourceAccountId, creditAccountId));
        await context.SaveChangesAsync();

        var purchases = await repository.GetByDateRangeAndSpendAccountAsync(from, to, creditAccountId);

        Assert.Empty(purchases);
    }

    [Fact]
    public async Task BothMethods_ReturnOnlyTheirOwnType_WhenBothExistInTheSameRangeForTheSameCard()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var creditAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var sourceAccountId = Guid.NewGuid();
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 9);

        context.Set<Transaction>().Add(new CreditCardPurchase(new DateOnly(2026, 9, 5), 150m, creditAccountId, categoryId));
        context.Set<Transaction>().Add(new CreditCardPayment(new DateOnly(2026, 9, 3), 300m, sourceAccountId, creditAccountId));
        await context.SaveChangesAsync();

        var purchases = await repository.GetByDateRangeAndSpendAccountAsync(from, to, creditAccountId);
        var payments = await repository.GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(from, to, creditAccountId);

        var purchase = Assert.Single(purchases);
        Assert.IsType<CreditCardPurchase>(purchase);
        Assert.Equal(150m, purchase.Amount);

        var payment = Assert.Single(payments);
        Assert.Equal(300m, payment.Amount);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
