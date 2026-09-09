using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.CreditAccounts;

public sealed class CreditCardTests
{
    private static CreditCard CreateCard(
        decimal creditLimit = 1000m,
        decimal openingAmountOwed = 0m,
        int statementCutOffDay = 15,
        int paymentDueDay = 5,
        string? lastFourDigits = "1234",
        decimal? annualInterestRate = null,
        decimal? monthlyInterestRate = null) =>
        new(
            "Visa Signature",
            CurrencyCode.USD,
            "Bank of Example",
            creditLimit,
            statementCutOffDay,
            paymentDueDay,
            openingAmountOwed,
            lastFourDigits,
            annualInterestRate,
            monthlyInterestRate);

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CreditCard(" ", CurrencyCode.USD, "Bank of Example", 1000m, 15, 5));
    }

    [Fact]
    public void Constructor_WithEmptyIssuer_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CreditCard("Visa Signature", CurrencyCode.USD, " ", 1000m, 15, 5));
    }

    [Fact]
    public void Constructor_WithNonPositiveCreditLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CreditCard("Visa Signature", CurrencyCode.USD, "Bank of Example", 0m, 15, 5));
    }

    [Fact]
    public void Constructor_WithNegativeOpeningAmountOwed_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(openingAmountOwed: -1m));
    }

    [Fact]
    public void Constructor_WithLastFourDigitsTooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateCard(lastFourDigits: "123"));
    }

    [Fact]
    public void Constructor_WithLastFourDigitsTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateCard(lastFourDigits: "12345"));
    }

    [Fact]
    public void Constructor_WithLastFourDigitsNonNumeric_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateCard(lastFourDigits: "12ab"));
    }

    [Fact]
    public void Constructor_WithCutOffDayBelowRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(statementCutOffDay: 0));
    }

    [Fact]
    public void Constructor_WithCutOffDayAboveRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(statementCutOffDay: 32));
    }

    [Fact]
    public void Constructor_WithPaymentDueDayBelowRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(paymentDueDay: 0));
    }

    [Fact]
    public void Constructor_WithPaymentDueDayAboveRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(paymentDueDay: 32));
    }

    [Fact]
    public void Constructor_WithNegativeAnnualInterestRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(annualInterestRate: -0.01m));
    }

    [Fact]
    public void Constructor_WithNegativeMonthlyInterestRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCard(monthlyInterestRate: -0.01m));
    }

    [Fact]
    public void AvailableCredit_ComputesLimitMinusAmountOwed()
    {
        var card = CreateCard(creditLimit: 1000m, openingAmountOwed: 300m);

        Assert.Equal(700m, card.AvailableCredit);
    }

    [Fact]
    public void AvailableCredit_WhenAmountOwedExceedsLimit_IsNegativeAndDoesNotThrow()
    {
        var card = CreateCard(creditLimit: 500m, openingAmountOwed: 0m);

        card.RegisterCharge(600m);

        Assert.Equal(-100m, card.AvailableCredit);
    }

    [Fact]
    public void RegisterCharge_IncreasesAmountOwed()
    {
        var card = CreateCard(openingAmountOwed: 100m);

        card.RegisterCharge(50m);

        Assert.Equal(150m, card.AmountOwed);
    }

    [Fact]
    public void RegisterCharge_WithZeroAmount_Throws()
    {
        var card = CreateCard();

        Assert.Throws<ArgumentOutOfRangeException>(() => card.RegisterCharge(0m));
    }

    [Fact]
    public void RegisterCharge_WithNegativeAmount_Throws()
    {
        var card = CreateCard();

        Assert.Throws<ArgumentOutOfRangeException>(() => card.RegisterCharge(-10m));
    }

    [Fact]
    public void RegisterPayment_DecreasesAmountOwed()
    {
        var card = CreateCard(openingAmountOwed: 200m);

        card.RegisterPayment(80m);

        Assert.Equal(120m, card.AmountOwed);
    }

    [Fact]
    public void RegisterPayment_WithZeroAmount_Throws()
    {
        var card = CreateCard(openingAmountOwed: 200m);

        Assert.Throws<ArgumentOutOfRangeException>(() => card.RegisterPayment(0m));
    }

    [Fact]
    public void RegisterPayment_WithNegativeAmount_Throws()
    {
        var card = CreateCard(openingAmountOwed: 200m);

        Assert.Throws<ArgumentOutOfRangeException>(() => card.RegisterPayment(-10m));
    }

    [Fact]
    public void RegisterPayment_ExceedingAmountOwed_Throws()
    {
        var card = CreateCard(openingAmountOwed: 200m);

        Assert.Throws<InvalidOperationException>(() => card.RegisterPayment(200.01m));
    }

    [Fact]
    public void Deactivate_ThenReactivate_RestoresIsActive()
    {
        var card = CreateCard();

        card.Deactivate();
        Assert.False(card.IsActive);

        card.Reactivate();
        Assert.True(card.IsActive);
    }

    [Fact]
    public void GetCutOffDateOnOrAfter_ShortMonthClamps_ThenNextCycleDoesNotStayStuckClamped()
    {
        var card = CreateCard(statementCutOffDay: 31);

        // The search starts within February 2026 (not a leap year) — the 31st doesn't exist there,
        // so the cut-off clamps to the 28th.
        var firstCycleEnd = card.GetCutOffDateOnOrAfter(new DateOnly(2026, 2, 1));
        Assert.Equal(new DateOnly(2026, 2, 28), firstCycleEnd);

        // The NEXT cycle, starting the day after the clamped date, must correctly recompute the
        // 31st for March rather than staying stuck at 28 (proves no permanent drift).
        var secondCycleEnd = card.GetCutOffDateOnOrAfter(firstCycleEnd.AddDays(1));
        Assert.Equal(new DateOnly(2026, 3, 31), secondCycleEnd);
    }

    [Fact]
    public void GetPaymentDueDateForCycleEndingOn_DueDayAfterCutOffDay_RollsToNextMonth()
    {
        var card = CreateCard(statementCutOffDay: 25, paymentDueDay: 10);

        var dueDate = card.GetPaymentDueDateForCycleEndingOn(new DateOnly(2026, 9, 25));

        Assert.Equal(new DateOnly(2026, 10, 10), dueDate);
    }

    [Fact]
    public void GetPaymentDueDateForCycleEndingOn_DueDayBeforeCutOffDay_StaysInSameMonth()
    {
        var card = CreateCard(statementCutOffDay: 5, paymentDueDay: 20);

        var dueDate = card.GetPaymentDueDateForCycleEndingOn(new DateOnly(2026, 9, 5));

        Assert.Equal(new DateOnly(2026, 9, 20), dueDate);
    }
}
