using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Reporting;

/// <summary>
/// Covers <see cref="MonthlySpendingTrendCalculator"/> (README §40 "Monthly expenses trend" report,
/// slice 1) — it must reuse <see cref="SpendingCalculator"/>'s exact spend-filtering rules (only
/// <see cref="Transaction.CountsAsExpense"/> counts, never blend currencies) while grouping by
/// calendar month instead of just currency.
/// </summary>
public sealed class MonthlySpendingTrendCalculatorTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Calculate_GroupsExpensesByCurrencyYearAndMonth()
    {
        var account = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 0m);
        var januaryExpense1 = new Expense(new DateOnly(2026, 1, 5), 30m, account.Id, CategoryId);
        var januaryExpense2 = new Expense(new DateOnly(2026, 1, 20), 20m, account.Id, CategoryId);
        var februaryExpense = new Expense(new DateOnly(2026, 2, 1), 50m, account.Id, CategoryId);

        var currencyMap = AccountCurrencyMapBuilder.Build([account], []);
        var summary = new MonthlySpendingTrendCalculator().Calculate(
            [januaryExpense1, januaryExpense2, februaryExpense], currencyMap);

        Assert.Equal(50m, summary.SpentByCurrencyAndMonth[(CurrencyCode.USD, 2026, 1)]);
        Assert.Equal(50m, summary.SpentByCurrencyAndMonth[(CurrencyCode.USD, 2026, 2)]);
    }

    [Fact]
    public void Calculate_ExcludesTransfersAndOtherNonExpenseMovements()
    {
        var source = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 0m);
        var destination = new BankAccount("Bank", CurrencyCode.USD, openingBalance: 0m, bankName: "Bank");
        var transfer = new Transfer(new DateOnly(2026, 3, 10), 100m, source.Id, destination.Id);

        var currencyMap = AccountCurrencyMapBuilder.Build([source, destination], []);
        var summary = new MonthlySpendingTrendCalculator().Calculate([transfer], currencyMap);

        Assert.Empty(summary.SpentByCurrencyAndMonth);
    }

    [Fact]
    public void Calculate_NeverBlendsCurrenciesForTheSameMonth()
    {
        var usdAccount = new CashAccount("USD Wallet", CurrencyCode.USD, openingBalance: 0m);
        var mxnAccount = new CashAccount("MXN Wallet", CurrencyCode.MXN, openingBalance: 0m);
        var usdExpense = new Expense(new DateOnly(2026, 4, 1), 20m, usdAccount.Id, CategoryId);
        var mxnExpense = new Expense(new DateOnly(2026, 4, 2), 20m, mxnAccount.Id, CategoryId);

        var currencyMap = AccountCurrencyMapBuilder.Build([usdAccount, mxnAccount], []);
        var summary = new MonthlySpendingTrendCalculator().Calculate([usdExpense, mxnExpense], currencyMap);

        Assert.Equal(20m, summary.SpentByCurrencyAndMonth[(CurrencyCode.USD, 2026, 4)]);
        Assert.Equal(20m, summary.SpentByCurrencyAndMonth[(CurrencyCode.MXN, 2026, 4)]);
    }

    [Fact]
    public void Calculate_SkipsExpenseWhoseAccountIsNotInTheCurrencyMap()
    {
        var account = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 0m);
        var expense = new Expense(new DateOnly(2026, 5, 1), 40m, account.Id, CategoryId);

        var summary = new MonthlySpendingTrendCalculator().Calculate([expense], new Dictionary<Guid, CurrencyCode>());

        Assert.Empty(summary.SpentByCurrencyAndMonth);
    }

    [Fact]
    public void Calculate_CountsCreditCardPurchaseAgainstItsCreditAccountCurrency()
    {
        var creditAccount = new Domain.CreditAccounts.CreditCard(
            "Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 15, paymentDueDay: 5);
        var purchase = new CreditCardPurchase(new DateOnly(2026, 6, 1), 75m, creditAccount.Id, CategoryId);

        var currencyMap = AccountCurrencyMapBuilder.Build([], [creditAccount]);
        var summary = new MonthlySpendingTrendCalculator().Calculate([purchase], currencyMap);

        Assert.Equal(75m, summary.SpentByCurrencyAndMonth[(CurrencyCode.USD, 2026, 6)]);
    }
}
