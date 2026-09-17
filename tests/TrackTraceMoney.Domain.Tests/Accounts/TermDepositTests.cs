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
            Guid.NewGuid(),
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

    [Fact]
    public void Constructor_WithEmptyInstitutionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TermDeposit(
                "12-Month CD",
                CurrencyCode.USD,
                Guid.Empty,
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
    public void Constructor_SetsInstitutionId()
    {
        var institutionId = Guid.NewGuid();

        var termDeposit = new TermDeposit(
            "12-Month CD",
            CurrencyCode.USD,
            institutionId,
            10000m,
            10000m,
            0.05m,
            TermDepositRateType.Nominal,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly,
            isCompounding: true,
            autoRenewal: false);

        Assert.Equal(institutionId, termDeposit.InstitutionId);
    }

    [Fact]
    public void SetInstitutionId_UpdatesInstitutionId()
    {
        var termDeposit = CreateTermDeposit();
        var newInstitutionId = Guid.NewGuid();

        termDeposit.SetInstitutionId(newInstitutionId);

        Assert.Equal(newInstitutionId, termDeposit.InstitutionId);
    }

    [Fact]
    public void SetInstitutionId_WithEmptyGuid_Throws()
    {
        var termDeposit = CreateTermDeposit();

        Assert.Throws<ArgumentException>(() => termDeposit.SetInstitutionId(Guid.Empty));
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

    private static TermDeposit CreateAutoRenewingTermDeposit(
        decimal openingBalance = 10000m,
        DateOnly? startDate = null,
        DateOnly? maturityDate = null) =>
        new(
            "12-Month CD",
            CurrencyCode.USD,
            Guid.NewGuid(),
            initialPrincipal: openingBalance,
            openingBalance,
            0.05m,
            TermDepositRateType.Nominal,
            startDate ?? new DateOnly(2026, 1, 1),
            maturityDate ?? new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly,
            isCompounding: true,
            autoRenewal: true);

    [Fact]
    public void RenewAtMaturity_CreatesNewTermDeposit_WithBalanceAsInitialPrincipal()
    {
        var termDeposit = CreateAutoRenewingTermDeposit(openingBalance: 10000m);
        termDeposit.RecordInterestReceived(200m); // Balance is now 10200m, InterestReceived is 200m.

        var renewal = termDeposit.RenewAtMaturity(termDeposit.MaturityDate);

        // The new InitialPrincipal/Balance equals the old Balance (10200m) directly — NOT
        // InitialPrincipal + InterestReceived (which would also be 10200m here, so this test alone
        // wouldn't distinguish the two formulas; the divergence case is covered separately below).
        Assert.Equal(10200m, renewal.InitialPrincipal);
        Assert.Equal(10200m, renewal.Balance);
    }

    [Fact]
    public void RenewAtMaturity_UsesBalanceNotInitialPrincipalPlusInterestReceived_WhenTheyDiverge()
    {
        var termDeposit = CreateAutoRenewingTermDeposit(openingBalance: 10000m);
        // Simulate the ledger's Balance diverging from InitialPrincipal + InterestReceived (e.g. via a
        // Transfer into this account bypassing RecordInterestReceived) by crediting through the base
        // FinancialAccount API rather than RecordInterestReceived.
        termDeposit.RecordInterestReceived(200m); // InterestReceived = 200m, Balance = 10200m.
        termDeposit.Credit(500m); // Balance = 10700m, InterestReceived still 200m (bypasses RecordInterestReceived).

        var renewal = termDeposit.RenewAtMaturity(termDeposit.MaturityDate);

        // InitialPrincipal (10000) + InterestReceived (200) = 10200, which must NOT be what's used.
        Assert.Equal(10700m, renewal.InitialPrincipal);
        Assert.NotEqual(termDeposit.InitialPrincipal + termDeposit.InterestReceived, renewal.InitialPrincipal);
    }

    [Fact]
    public void RenewAtMaturity_CopiesInstitutionRateRateTypeInterestFrequencyIsCompoundingAutoRenewal()
    {
        var termDeposit = CreateAutoRenewingTermDeposit();

        var renewal = termDeposit.RenewAtMaturity(termDeposit.MaturityDate);

        Assert.Equal(termDeposit.Name, renewal.Name);
        Assert.Equal(termDeposit.Currency, renewal.Currency);
        Assert.Equal(termDeposit.InstitutionId, renewal.InstitutionId);
        Assert.Equal(termDeposit.Rate, renewal.Rate);
        Assert.Equal(termDeposit.RateType, renewal.RateType);
        Assert.Equal(termDeposit.InterestFrequency, renewal.InterestFrequency);
        Assert.Equal(termDeposit.IsCompounding, renewal.IsCompounding);
        Assert.Equal(termDeposit.AutoRenewal, renewal.AutoRenewal);
        Assert.Equal(termDeposit.Notes, renewal.Notes);
    }

    [Fact]
    public void RenewAtMaturity_SetsNewStartDateToOldMaturityDate_AndPreservesTermLengthInDays()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var maturityDate = new DateOnly(2027, 1, 1); // 365 days.
        var termDeposit = CreateAutoRenewingTermDeposit(startDate: startDate, maturityDate: maturityDate);

        // Renewing a few days late must not shorten the new term or penalize the user.
        var lateAsOfDate = maturityDate.AddDays(5);
        var renewal = termDeposit.RenewAtMaturity(lateAsOfDate);

        Assert.Equal(maturityDate, renewal.StartDate);

        var originalTermLengthDays = maturityDate.DayNumber - startDate.DayNumber;
        var renewalTermLengthDays = renewal.MaturityDate.DayNumber - renewal.StartDate.DayNumber;
        Assert.Equal(originalTermLengthDays, renewalTermLengthDays);
    }

    [Fact]
    public void RenewAtMaturity_ResetsEstimatedInterestToNull_AndInterestReceivedToZero()
    {
        var termDeposit = CreateAutoRenewingTermDeposit();
        termDeposit.RecordInterestReceived(123.45m);

        var renewal = termDeposit.RenewAtMaturity(termDeposit.MaturityDate);

        Assert.Null(renewal.EstimatedInterest);
        Assert.Equal(0m, renewal.InterestReceived);
    }

    [Fact]
    public void RenewAtMaturity_DeactivatesTheOriginalDeposit()
    {
        var termDeposit = CreateAutoRenewingTermDeposit();
        Assert.True(termDeposit.IsActive);

        termDeposit.RenewAtMaturity(termDeposit.MaturityDate);

        Assert.False(termDeposit.IsActive);
    }

    [Fact]
    public void RenewAtMaturity_WhenAutoRenewalFalse_Throws()
    {
        var termDeposit = CreateTermDeposit(); // autoRenewal: false by default.

        Assert.Throws<InvalidOperationException>(() => termDeposit.RenewAtMaturity(termDeposit.MaturityDate));
    }

    [Fact]
    public void RenewAtMaturity_WhenCalledBeforeMaturityDate_Throws()
    {
        var termDeposit = CreateAutoRenewingTermDeposit(maturityDate: new DateOnly(2027, 1, 1));

        Assert.Throws<InvalidOperationException>(
            () => termDeposit.RenewAtMaturity(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void RenewAtMaturity_WhenBalanceIsZeroOrNegative_Throws()
    {
        var termDeposit = CreateAutoRenewingTermDeposit(openingBalance: 100m);
        termDeposit.Debit(100m); // Balance is now 0.

        Assert.Throws<InvalidOperationException>(() => termDeposit.RenewAtMaturity(termDeposit.MaturityDate));
    }
}
