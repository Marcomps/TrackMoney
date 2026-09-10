using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.Accounts;

/// <summary>
/// Confirms every <see cref="FinancialAccount"/> subtype's <see cref="FinancialAccount.CountsAsAvailableBalance"/>
/// answer, since a wrong answer here silently corrupts Dashboard Tile 1's "available balance" total
/// (see DashboardViewModel's Tile 1 remarks) rather than throwing.
/// </summary>
public sealed class FinancialAccountCountsAsAvailableBalanceTests
{
    [Fact]
    public void CashAccount_CountsAsAvailableBalance_IsTrue()
    {
        var account = new CashAccount("Wallet", CurrencyCode.USD);

        Assert.True(account.CountsAsAvailableBalance);
    }

    [Fact]
    public void BankAccount_CountsAsAvailableBalance_IsTrue()
    {
        var account = new BankAccount("Checking", CurrencyCode.USD);

        Assert.True(account.CountsAsAvailableBalance);
    }

    [Fact]
    public void SavingsAccount_CountsAsAvailableBalance_IsTrue()
    {
        var account = new SavingsAccount("Emergency Fund", CurrencyCode.USD);

        Assert.True(account.CountsAsAvailableBalance);
    }

    [Fact]
    public void TermDeposit_CountsAsAvailableBalance_IsFalse()
    {
        var termDeposit = new TermDeposit(
            "12-Month CD",
            CurrencyCode.USD,
            "Bank of Example",
            10000m,
            10000m,
            0.05m,
            TermDepositRateType.Nominal,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly,
            isCompounding: true,
            autoRenewal: false);

        Assert.False(termDeposit.CountsAsAvailableBalance);
    }
}
