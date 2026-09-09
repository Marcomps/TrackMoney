using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Domain.Tests.CreditAccounts;

public sealed class CreditCardStatementTests
{
    private static CreditCardStatement CreateStatement(
        DateOnly? cycleStartDate = null,
        DateOnly? cycleEndDate = null,
        decimal minimumPayment = 50m,
        decimal payInFullAmount = 200m) =>
        new(
            Guid.NewGuid(),
            cycleStartDate ?? new DateOnly(2026, 8, 26),
            cycleEndDate ?? new DateOnly(2026, 9, 25),
            minimumPayment,
            payInFullAmount);

    [Fact]
    public void Constructor_WithCycleEndBeforeCycleStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateStatement(
            cycleStartDate: new DateOnly(2026, 9, 25),
            cycleEndDate: new DateOnly(2026, 8, 26)));
    }

    [Fact]
    public void Constructor_WithNegativeMinimumPayment_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateStatement(minimumPayment: -1m));
    }

    [Fact]
    public void Constructor_WithNegativePayInFullAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateStatement(payInFullAmount: -1m));
    }

    [Fact]
    public void Constructor_WithMinimumPaymentGreaterThanPayInFullAmount_DoesNotThrow()
    {
        // Deliberately no cross-field enforcement — CLAUDE.md: these are independent, user-entered fields.
        var statement = CreateStatement(minimumPayment: 500m, payInFullAmount: 100m);

        Assert.Equal(500m, statement.MinimumPayment);
        Assert.Equal(100m, statement.PayInFullAmount);
    }

    [Fact]
    public void UpdateAmounts_ChangesOnlyTheTwoAmounts()
    {
        var statement = CreateStatement();
        var originalCreditAccountId = statement.CreditAccountId;
        var originalCycleStart = statement.CycleStartDate;
        var originalCycleEnd = statement.CycleEndDate;

        statement.UpdateAmounts(75m, 300m);

        Assert.Equal(75m, statement.MinimumPayment);
        Assert.Equal(300m, statement.PayInFullAmount);
        Assert.Equal(originalCreditAccountId, statement.CreditAccountId);
        Assert.Equal(originalCycleStart, statement.CycleStartDate);
        Assert.Equal(originalCycleEnd, statement.CycleEndDate);
    }

    [Fact]
    public void UpdateAmounts_WithNegativeMinimumPayment_Throws()
    {
        var statement = CreateStatement();

        Assert.Throws<ArgumentOutOfRangeException>(() => statement.UpdateAmounts(-1m, 100m));
    }

    [Fact]
    public void UpdateAmounts_WithNegativePayInFullAmount_Throws()
    {
        var statement = CreateStatement();

        Assert.Throws<ArgumentOutOfRangeException>(() => statement.UpdateAmounts(100m, -1m));
    }
}
