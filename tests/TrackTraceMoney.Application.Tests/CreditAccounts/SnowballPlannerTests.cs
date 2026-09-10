using TrackTraceMoney.Application.CreditAccounts;

namespace TrackTraceMoney.Application.Tests.CreditAccounts;

/// <summary>
/// README §20's Snowball strategy — worked numeric examples supplied by the product-owner scoping
/// pass for this slice. <see cref="SnowballPlanner"/> is a pure calculator (no repository/wall-clock
/// access), so every input — including <see cref="SnowballDebtInput.CreatedAtUtc"/> — is supplied
/// explicitly here.
/// </summary>
public sealed class SnowballPlannerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SnowballDebtInput MakeDebt(string name, decimal amountOwed, decimal minimumPayment, DateTimeOffset? createdAtUtc = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, amountOwed, minimumPayment, createdAtUtc ?? BaseTime);

    [Fact]
    public void Plan_ReadmeWorkedExample_LoanAndCard_TargetsSmallerCardBalance()
    {
        var loan = MakeDebt("Loan", amountOwed: 5000m, minimumPayment: 200m);
        var card = MakeDebt("Card", amountOwed: 250m, minimumPayment: 25m);

        var plan = new SnowballPlanner().Plan([loan, card], extraAvailable: 150m);

        var targetLine = Assert.Single(plan.Lines, l => l.IsCurrentTarget);
        Assert.Equal(card.CreditAccountId, targetLine.CreditAccountId);
        Assert.Equal(150m, targetLine.SuggestedExtra);
        Assert.Equal(175m, targetLine.SuggestedTotalPayment);
        Assert.Equal(225m, plan.TotalMinimums);
        Assert.Equal(0m, plan.UnallocatedExtra);
    }

    [Fact]
    public void Plan_ThreeDebtsNoOverflow_TargetsSmallestBalanceOnly()
    {
        var cardA = MakeDebt("Card A", amountOwed: 250m, minimumPayment: 25m);
        var cardB = MakeDebt("Card B", amountOwed: 800m, minimumPayment: 80m);
        var loan = MakeDebt("Loan", amountOwed: 5000m, minimumPayment: 200m);

        var plan = new SnowballPlanner().Plan([cardA, cardB, loan], extraAvailable: 150m);

        Assert.Equal([cardA.CreditAccountId, cardB.CreditAccountId, loan.CreditAccountId], plan.Lines.Select(l => l.CreditAccountId));

        var lineA = plan.Lines[0];
        var lineB = plan.Lines[1];
        var lineLoan = plan.Lines[2];

        Assert.True(lineA.IsCurrentTarget);
        Assert.Equal(150m, lineA.SuggestedExtra);
        Assert.Equal(175m, lineA.SuggestedTotalPayment);

        Assert.False(lineB.IsCurrentTarget);
        Assert.Equal(0m, lineB.SuggestedExtra);

        Assert.False(lineLoan.IsCurrentTarget);
        Assert.Equal(0m, lineLoan.SuggestedExtra);

        Assert.Equal(305m, plan.TotalMinimums);
        Assert.Equal(0m, plan.UnallocatedExtra);
    }

    [Fact]
    public void Plan_ThreeDebtsOverflow_CapsExtraAtHeadroomAndSuggestsNextTarget()
    {
        var cardA = MakeDebt("Card A", amountOwed: 250m, minimumPayment: 25m);
        var cardB = MakeDebt("Card B", amountOwed: 800m, minimumPayment: 80m);
        var loan = MakeDebt("Loan", amountOwed: 5000m, minimumPayment: 200m);

        var plan = new SnowballPlanner().Plan([cardA, cardB, loan], extraAvailable: 500m);

        var lineA = plan.Lines[0];
        Assert.True(lineA.IsCurrentTarget);
        Assert.Equal(225m, lineA.SuggestedExtra); // headroom = 250 - 25
        Assert.Equal(250m, lineA.SuggestedTotalPayment);

        Assert.Equal(275m, plan.UnallocatedExtra);
        Assert.Equal(cardB.CreditAccountId, plan.NextTargetAfterCurrentId);
    }

    [Fact]
    public void Plan_TieOnAmountOwed_EarlierCreatedAtUtcRanksFirstAndBecomesTarget()
    {
        var older = MakeDebt("Older Debt", amountOwed: 500m, minimumPayment: 50m, createdAtUtc: BaseTime);
        var newer = MakeDebt("Newer Debt", amountOwed: 500m, minimumPayment: 50m, createdAtUtc: BaseTime.AddDays(1));

        var plan = new SnowballPlanner().Plan([newer, older], extraAvailable: 100m);

        Assert.Equal(older.CreditAccountId, plan.Lines[0].CreditAccountId);
        Assert.True(plan.Lines[0].IsCurrentTarget);
        Assert.Equal(100m, plan.Lines[0].SuggestedExtra);
    }

    [Fact]
    public void Plan_EmptyDebts_ReturnsEmptyPlanWithAllExtraUnallocated()
    {
        var plan = new SnowballPlanner().Plan([], extraAvailable: 100m);

        Assert.True(plan.IsEmpty);
        Assert.Equal(0m, plan.TotalMinimums);
        Assert.Equal(0m, plan.ExtraAppliedToTarget);
        Assert.Equal(100m, plan.UnallocatedExtra);
        Assert.Null(plan.NextTargetAfterCurrentId);
    }

    [Fact]
    public void Plan_NegativeExtraAvailable_Throws()
    {
        var debt = MakeDebt("Debt", amountOwed: 500m, minimumPayment: 50m);

        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowballPlanner().Plan([debt], extraAvailable: -1m));
    }

    [Fact]
    public void Plan_SingleDebtOverflow_HasUnallocatedExtraButNoNextTarget()
    {
        var debt = MakeDebt("Only Debt", amountOwed: 100m, minimumPayment: 25m);

        var plan = new SnowballPlanner().Plan([debt], extraAvailable: 200m);

        Assert.True(plan.UnallocatedExtra > 0m);
        Assert.Null(plan.NextTargetAfterCurrentId);
    }
}
