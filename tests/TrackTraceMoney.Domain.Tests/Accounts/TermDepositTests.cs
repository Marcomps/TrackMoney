using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.Accounts;

public sealed class TermDepositTests
{
    private static TermDeposit CreateTermDeposit(
        decimal initialPrincipal = 10000m,
        decimal openingBalance = 10000m,
        decimal rate = 0.05m,
        DateOnly? startDate = null,
        DateOnly? maturityDate = null,
        decimal? estimatedInterest = null,
        decimal interestReceived = 0m) =>
        new(
            "12-Month CD",
            CurrencyCode.USD,
            "Bank of Example",
            initialPrincipal,
            openingBalance,
            rate,
            TermDepositRateType.Nominal,
            startDate ?? new DateOnly(2026, 1, 1),
            maturityDate ?? new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly,
            isCompounding: true,
            autoRenewal: false,
            estimatedInterest,
            interestReceived);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceInstitution_Throws(string institution)
    {
        Assert.Throws<ArgumentException>(() =>
            new TermDeposit(
                "12-Month CD",
                CurrencyCode.USD,
                institution,
                10000m,
                10000m,
                0.05m,
                TermDepositRateType.Nominal,
                new DateOnly(2026, 1, 1),
                new DateOnly(2027, 1, 1),
                TermDepositInterestFrequency.Monthly,
                isCompounding: true,
                autoRenewal: false));
    }

    [Fact]
    public void Constructor_WithNonPositiveInitialPrincipal_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTermDeposit(initialPrincipal: 0m));
    }

    [Fact]
    public void Constructor_WithNegativeInitialPrincipal_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTermDeposit(initialPrincipal: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeOpeningBalance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTermDeposit(openingBalance: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTermDeposit(rate: -0.01m));
    }

    [Fact]
    public void Constructor_WithMaturityDateEqualToStartDate_Throws()
    {
        var date = new DateOnly(2026, 6, 1);

        Assert.Throws<ArgumentException>(() => CreateTermDeposit(startDate: date, maturityDate: date));
    }

    [Fact]
    public void Constructor_WithMaturityDateBeforeStartDate_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateTermDeposit(
            startDate: new DateOnly(2026, 6, 1),
            maturityDate: new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void Constructor_WithNegativeEstimatedInterest_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTermDeposit(estimatedInterest: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeInterestReceived_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTermDeposit(interestReceived: -1m));
    }

    [Fact]
    public void RecordInterestReceived_IncreasesBalanceAndInterestReceived()
    {
        var termDeposit = CreateTermDeposit(openingBalance: 10000m, interestReceived: 0m);

        termDeposit.RecordInterestReceived(41.67m);

        Assert.Equal(10041.67m, termDeposit.Balance);
        Assert.Equal(41.67m, termDeposit.InterestReceived);
    }

    [Fact]
    public void RecordInterestReceived_CalledTwice_AccumulatesInterestReceived()
    {
        var termDeposit = CreateTermDeposit(openingBalance: 10000m, interestReceived: 0m);

        termDeposit.RecordInterestReceived(41.67m);
        termDeposit.RecordInterestReceived(41.67m);

        Assert.Equal(10083.34m, termDeposit.Balance);
        Assert.Equal(83.34m, termDeposit.InterestReceived);
    }

    [Fact]
    public void RecordInterestReceived_WithZeroAmount_Throws()
    {
        var termDeposit = CreateTermDeposit();

        Assert.Throws<ArgumentOutOfRangeException>(() => termDeposit.RecordInterestReceived(0m));
    }

    [Fact]
    public void RecordInterestReceived_WithNegativeAmount_Throws()
    {
        var termDeposit = CreateTermDeposit();

        Assert.Throws<ArgumentOutOfRangeException>(() => termDeposit.RecordInterestReceived(-1m));
    }
}
