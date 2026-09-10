using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.Accounts;

public sealed class InvestmentFundTests
{
    private static InvestmentFund CreateInvestmentFund(
        decimal contributions = 2000m,
        decimal openingBalance = 2084.50m,
        decimal withdrawals = 0m,
        decimal fees = 0m,
        DateOnly? investmentDate = null) =>
        new(
            "Growth Fund",
            CurrencyCode.USD,
            "Example Asset Management",
            investmentDate ?? new DateOnly(2026, 1, 1),
            contributions,
            openingBalance,
            withdrawals,
            fees);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceInstitution_Throws(string institution)
    {
        Assert.Throws<ArgumentException>(() =>
            new InvestmentFund(
                "Growth Fund",
                CurrencyCode.USD,
                institution,
                new DateOnly(2026, 1, 1),
                2000m,
                2084.50m));
    }

    [Fact]
    public void Constructor_WithOversizedInstitution_Throws()
    {
        var institution = new string('A', 201);

        Assert.Throws<ArgumentException>(() =>
            new InvestmentFund(
                "Growth Fund",
                CurrencyCode.USD,
                institution,
                new DateOnly(2026, 1, 1),
                2000m,
                2084.50m));
    }

    [Fact]
    public void Constructor_WithZeroContributions_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInvestmentFund(contributions: 0m));
    }

    [Fact]
    public void Constructor_WithNegativeContributions_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInvestmentFund(contributions: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeOpeningBalance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInvestmentFund(openingBalance: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeWithdrawals_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInvestmentFund(withdrawals: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeFees_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInvestmentFund(fees: -1m));
    }

    [Fact]
    public void GainAndReturnPercentage_MatchReadmeExample()
    {
        // README §23's own worked example: Initial investment $2,000, Current value $2,084.50 ->
        // Gain $84.50, Return 4.23% (0.04225 as a raw fraction, matching TermDeposit.Rate's convention).
        var fund = CreateInvestmentFund(contributions: 2000m, withdrawals: 0m, openingBalance: 2084.50m);

        Assert.Equal(84.50m, fund.Gain);
        Assert.Equal(0.04225m, fund.ReturnPercentage);
    }

    [Fact]
    public void ReturnPercentage_WhenContributionsEqualWithdrawals_IsNull()
    {
        var fund = CreateInvestmentFund(contributions: 2000m, withdrawals: 2000m, openingBalance: 0m);

        Assert.Null(fund.ReturnPercentage);
    }

    [Fact]
    public void GainAndReturnPercentage_WhenFundIsAtALoss_AreNegativeAndDoNotThrow()
    {
        var fund = CreateInvestmentFund(contributions: 2000m, withdrawals: 0m, openingBalance: 1500m);

        Assert.Equal(-500m, fund.Gain);
        Assert.Equal(-0.25m, fund.ReturnPercentage);
    }

    [Fact]
    public void RecordContribution_IncreasesBalanceAndContributions()
    {
        var fund = CreateInvestmentFund(contributions: 2000m, openingBalance: 2084.50m);

        fund.RecordContribution(500m);

        Assert.Equal(2584.50m, fund.Balance);
        Assert.Equal(2500m, fund.Contributions);
    }

    [Fact]
    public void RecordContribution_WithZeroAmount_Throws()
    {
        var fund = CreateInvestmentFund();

        Assert.Throws<ArgumentOutOfRangeException>(() => fund.RecordContribution(0m));
    }

    [Fact]
    public void RecordContribution_WithNegativeAmount_Throws()
    {
        var fund = CreateInvestmentFund();

        Assert.Throws<ArgumentOutOfRangeException>(() => fund.RecordContribution(-1m));
    }

    [Fact]
    public void RecordWithdrawal_DecreasesBalanceAndIncreasesWithdrawals()
    {
        var fund = CreateInvestmentFund(contributions: 2000m, openingBalance: 2084.50m, withdrawals: 0m);

        fund.RecordWithdrawal(84.50m);

        Assert.Equal(2000m, fund.Balance);
        Assert.Equal(84.50m, fund.Withdrawals);
    }

    [Fact]
    public void RecordWithdrawal_WithZeroAmount_Throws()
    {
        var fund = CreateInvestmentFund();

        Assert.Throws<ArgumentOutOfRangeException>(() => fund.RecordWithdrawal(0m));
    }

    [Fact]
    public void RecordWithdrawal_WithNegativeAmount_Throws()
    {
        var fund = CreateInvestmentFund();

        Assert.Throws<ArgumentOutOfRangeException>(() => fund.RecordWithdrawal(-1m));
    }

    [Fact]
    public void RecordValuation_WithNegativeValue_Throws()
    {
        var fund = CreateInvestmentFund();

        Assert.Throws<ArgumentOutOfRangeException>(() => fund.RecordValuation(-1m));
    }

    [Fact]
    public void RecordValuation_WithHigherValue_IncreasesBalance()
    {
        var fund = CreateInvestmentFund(openingBalance: 2084.50m);

        fund.RecordValuation(2200m);

        Assert.Equal(2200m, fund.Balance);
    }

    [Fact]
    public void RecordValuation_WithLowerValue_DecreasesBalance()
    {
        var fund = CreateInvestmentFund(openingBalance: 2084.50m);

        fund.RecordValuation(1900m);

        Assert.Equal(1900m, fund.Balance);
    }

    [Fact]
    public void CountsAsAvailableBalance_IsFalse()
    {
        var fund = CreateInvestmentFund();

        Assert.False(fund.CountsAsAvailableBalance);
    }
}
