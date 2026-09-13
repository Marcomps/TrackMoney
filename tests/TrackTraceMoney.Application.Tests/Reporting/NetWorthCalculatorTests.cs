using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Tests.Reporting;

/// <summary>
/// README §24's net worth formula: sum of asset balances minus sum of liability balances, per
/// currency, never blended across currencies (CLAUDE.md hard constraint).
/// </summary>
public sealed class NetWorthCalculatorTests
{
    [Fact]
    public void Calculate_SumsAllFinancialAccountTypesAsAssets_IgnoringCountsAsAvailableBalance()
    {
        // TermDeposit and InvestmentFund both override CountsAsAvailableBalance to false (they're
        // excluded from Dashboard Tile 1's "available balance"), but net worth must still include
        // them — this is the explicit proof that NetWorthCalculator does NOT consult that flag.
        var cash = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m);
        var termDeposit = new TermDeposit(
            "CD", CurrencyCode.USD, "Bank", initialPrincipal: 500m, openingBalance: 500m, rate: 0.03m,
            TermDepositRateType.Nominal, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.AtMaturity, isCompounding: false, autoRenewal: false);
        var investmentFund = new InvestmentFund(
            "Index Fund", CurrencyCode.USD, "Broker", new DateOnly(2026, 1, 1),
            contributions: 300m, openingBalance: 350m);

        Assert.False(termDeposit.CountsAsAvailableBalance);
        Assert.False(investmentFund.CountsAsAvailableBalance);

        var summary = new NetWorthCalculator().Calculate([cash, termDeposit, investmentFund], []);

        Assert.Equal(950m, summary.ByCurrency[CurrencyCode.USD].TotalAssets);
    }

    [Fact]
    public void Calculate_SumsCreditCardAndLoanAsLiabilities()
    {
        var creditCard = new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m,
            statementCutOffDay: 1, paymentDueDay: 15, openingAmountOwed: 200m);
        var loan = new Loan("Car Loan", CurrencyCode.USD, "Bank", LoanKind.AutoLoan,
            originalAmount: 10000m, currentBalance: 4000m, interestRate: 0.1m, LoanRateType.Fixed,
            monthlyInstallment: 300m, nextPaymentDate: new DateOnly(2026, 2, 1), requiredPayment: 300m);

        var summary = new NetWorthCalculator().Calculate([], [creditCard, loan]);

        Assert.Equal(4200m, summary.ByCurrency[CurrencyCode.USD].TotalLiabilities);
    }

    [Fact]
    public void Calculate_CurrencyPresentOnlyOnOneSide_StillYieldsARowWithTheOtherSideAtZero()
    {
        var cash = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m);

        var summary = new NetWorthCalculator().Calculate([cash], []);

        var row = summary.ByCurrency[CurrencyCode.USD];
        Assert.Equal(100m, row.TotalAssets);
        Assert.Equal(0m, row.TotalLiabilities);
        Assert.Equal(100m, row.NetWorth);
    }

    [Fact]
    public void Calculate_MultiCurrencyIsolation_NeverCombinesUsdAssetAndMxnLiability()
    {
        var usdCash = new CashAccount("Wallet USD", CurrencyCode.USD, openingBalance: 500m);
        var mxnCard = new CreditCard("Tarjeta", CurrencyCode.MXN, "Banco", creditLimit: 5000m,
            statementCutOffDay: 1, paymentDueDay: 15, openingAmountOwed: 1000m);

        var summary = new NetWorthCalculator().Calculate([usdCash], [mxnCard]);

        Assert.Equal(500m, summary.ByCurrency[CurrencyCode.USD].TotalAssets);
        Assert.Equal(0m, summary.ByCurrency[CurrencyCode.USD].TotalLiabilities);
        Assert.Equal(0m, summary.ByCurrency[CurrencyCode.MXN].TotalAssets);
        Assert.Equal(1000m, summary.ByCurrency[CurrencyCode.MXN].TotalLiabilities);
    }

    [Fact]
    public void Calculate_EmptyInputs_ReturnsEmptyByCurrency()
    {
        var summary = new NetWorthCalculator().Calculate([], []);

        Assert.Empty(summary.ByCurrency);
    }

    [Fact]
    public void Calculate_NetWorthCanBeNegativeForACurrency()
    {
        var cash = new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m);
        var creditCard = new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 5000m,
            statementCutOffDay: 1, paymentDueDay: 15, openingAmountOwed: 900m);

        var summary = new NetWorthCalculator().Calculate([cash], [creditCard]);

        Assert.Equal(-800m, summary.ByCurrency[CurrencyCode.USD].NetWorth);
    }
}
