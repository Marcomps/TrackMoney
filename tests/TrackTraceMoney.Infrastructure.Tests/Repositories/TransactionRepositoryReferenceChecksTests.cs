using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Runs <c>TransactionRepository.HasAnyTransactionReferencingFinancialAccountAsync</c>/
/// <c>HasAnyTransactionReferencingCreditAccountAsync</c> (edit/delete slice spec §0) against a real
/// SQLite-backed <see cref="TrackTraceMoneyDbContext"/> rather than an in-memory fake, so a query that
/// fails to translate against the TPH-mapped <c>Transactions</c> table — or checks the wrong column for
/// a given subtype — would actually be caught, not just assumed correct from reading the LINQ.
/// </summary>
public sealed class TransactionRepositoryReferenceChecksTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public TransactionRepositoryReferenceChecksTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task HasAnyTransactionReferencingFinancialAccountAsync_NoTransactions_ReturnsFalse()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        Assert.False(await repository.HasAnyTransactionReferencingFinancialAccountAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Expense")]
    [InlineData("Income")]
    [InlineData("TransferSource")]
    [InlineData("TransferDestination")]
    [InlineData("CreditCardPaymentSource")]
    [InlineData("LoanPaymentSource")]
    [InlineData("InvestmentContributionSource")]
    [InlineData("InvestmentContributionDestination")]
    [InlineData("InvestmentWithdrawalSource")]
    [InlineData("InvestmentWithdrawalDestination")]
    [InlineData("InterestIncome")]
    [InlineData("Reimbursement")]
    public async Task HasAnyTransactionReferencingFinancialAccountAsync_MatchesEveryFinancialAccountShapedColumn(string kind)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var accountId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var today = new DateOnly(2026, 9, 16);

        Transaction transaction = kind switch
        {
            "Expense" => new Expense(today, 10m, accountId, Guid.NewGuid()),
            "Income" => new Income(today, 10m, accountId, Guid.NewGuid()),
            "TransferSource" => new Transfer(today, 10m, accountId, otherId),
            "TransferDestination" => new Transfer(today, 10m, otherId, accountId),
            "CreditCardPaymentSource" => new CreditCardPayment(today, 10m, accountId, otherId),
            "LoanPaymentSource" => new LoanPayment(today, 10m, accountId, otherId),
            "InvestmentContributionSource" => new InvestmentContribution(today, 10m, accountId, otherId),
            "InvestmentContributionDestination" => new InvestmentContribution(today, 10m, otherId, accountId),
            "InvestmentWithdrawalSource" => new InvestmentWithdrawal(today, 10m, accountId, otherId),
            "InvestmentWithdrawalDestination" => new InvestmentWithdrawal(today, 10m, otherId, accountId),
            "InterestIncome" => new InterestIncome(today, 10m, accountId),
            "Reimbursement" => new Reimbursement(today, 10m, accountId, Guid.NewGuid()),
            _ => throw new InvalidOperationException($"Unknown kind '{kind}'."),
        };

        context.Set<Transaction>().Add(transaction);
        await context.SaveChangesAsync();

        Assert.True(await repository.HasAnyTransactionReferencingFinancialAccountAsync(accountId));
        Assert.False(await repository.HasAnyTransactionReferencingFinancialAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasAnyTransactionReferencingCreditAccountAsync_NoTransactions_ReturnsFalse()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        Assert.False(await repository.HasAnyTransactionReferencingCreditAccountAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("CreditCardPurchase")]
    [InlineData("CreditCardPayment")]
    [InlineData("LoanPayment")]
    public async Task HasAnyTransactionReferencingCreditAccountAsync_MatchesEveryCreditAccountShapedColumn(string kind)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var creditAccountId = Guid.NewGuid();
        var today = new DateOnly(2026, 9, 16);

        Transaction transaction = kind switch
        {
            "CreditCardPurchase" => new CreditCardPurchase(today, 10m, creditAccountId, Guid.NewGuid()),
            "CreditCardPayment" => new CreditCardPayment(today, 10m, Guid.NewGuid(), creditAccountId),
            "LoanPayment" => new LoanPayment(today, 10m, Guid.NewGuid(), creditAccountId),
            _ => throw new InvalidOperationException($"Unknown kind '{kind}'."),
        };

        context.Set<Transaction>().Add(transaction);
        await context.SaveChangesAsync();

        Assert.True(await repository.HasAnyTransactionReferencingCreditAccountAsync(creditAccountId));
        Assert.False(await repository.HasAnyTransactionReferencingCreditAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasAnyTransactionReferencingCategoryAsync_NoTransactions_ReturnsFalse()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        Assert.False(await repository.HasAnyTransactionReferencingCategoryAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Expense")]
    [InlineData("Income")]
    [InlineData("CreditCardPurchase")]
    public async Task HasAnyTransactionReferencingCategoryAsync_MatchesEveryCategoryShapedColumn(string kind)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var categoryId = Guid.NewGuid();
        var today = new DateOnly(2026, 9, 21);

        Transaction transaction = kind switch
        {
            "Expense" => new Expense(today, 10m, Guid.NewGuid(), categoryId),
            "Income" => new Income(today, 10m, Guid.NewGuid(), categoryId),
            "CreditCardPurchase" => new CreditCardPurchase(today, 10m, Guid.NewGuid(), categoryId),
            _ => throw new InvalidOperationException($"Unknown kind '{kind}'."),
        };

        context.Set<Transaction>().Add(transaction);
        await context.SaveChangesAsync();

        Assert.True(await repository.HasAnyTransactionReferencingCategoryAsync(categoryId));
        Assert.False(await repository.HasAnyTransactionReferencingCategoryAsync(Guid.NewGuid()));
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
