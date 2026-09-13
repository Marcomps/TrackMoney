using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Reporting;

/// <summary>
/// Coverage for <see cref="IncomeCalculator"/>, including the Phase 3 slice 4 addition of
/// <see cref="InterestIncome"/> — the calculator drives entirely off <see cref="Transaction.CountsAsIncome"/>/
/// <see cref="Transaction.IncomeAccountId"/>, so no type-specific pattern matching was needed in
/// production code for this new type to be picked up (CLAUDE.md's non-obvious-domain-rules section).
/// </summary>
public sealed class IncomeCalculatorTests
{
    [Fact]
    public void Calculate_IncomeAndInterestIncome_SameCurrency_SumTogether()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m);
        var categoryId = Guid.NewGuid();
        var income = new Income(DateOnly.FromDateTime(DateTime.Today), 500m, account.Id, categoryId);
        var interestIncome = new InterestIncome(DateOnly.FromDateTime(DateTime.Today), 41.67m, account.Id);

        var currencyMap = AccountCurrencyMapBuilder.Build([account], []);

        var summary = new IncomeCalculator().Calculate([income, interestIncome], currencyMap);

        Assert.Equal(541.67m, summary.TotalIncomeByCurrency[CurrencyCode.USD]);
    }

    [Fact]
    public void Calculate_InterestIncomeAccountNotInCurrencyMap_IsSkippedNotThrown()
    {
        // Mirrors SpendingCalculator's deliberate lookup-miss skip (see its own doc comment) — an
        // incomplete currency map must never throw, it must simply omit that transaction from the total.
        var interestIncome = new InterestIncome(DateOnly.FromDateTime(DateTime.Today), 41.67m, Guid.NewGuid());
        var emptyCurrencyMap = AccountCurrencyMapBuilder.Build([], []);

        var summary = new IncomeCalculator().Calculate([interestIncome], emptyCurrencyMap);

        Assert.Empty(summary.TotalIncomeByCurrency);
    }

    [Fact]
    public void Calculate_TransferAndInvestmentContribution_NeverCountAsIncome()
    {
        var source = new BankAccount("Checking", CurrencyCode.USD, openingBalance: 100m);
        var destination = new BankAccount("Savings", CurrencyCode.USD, openingBalance: 0m);
        var transfer = new Transfer(DateOnly.FromDateTime(DateTime.Today), 50m, source.Id, destination.Id);
        var contribution = new InvestmentContribution(DateOnly.FromDateTime(DateTime.Today), 50m, source.Id, destination.Id);

        var currencyMap = AccountCurrencyMapBuilder.Build([source, destination], []);

        var summary = new IncomeCalculator().Calculate([transfer, contribution], currencyMap);

        Assert.Empty(summary.TotalIncomeByCurrency);
    }
}
