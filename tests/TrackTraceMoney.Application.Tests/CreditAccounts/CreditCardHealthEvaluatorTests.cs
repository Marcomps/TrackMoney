using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Tests.CreditAccounts;

/// <summary>
/// README §18 card health semáforo — worked numeric examples supplied by the product-owner scoping
/// pass for this slice. "Today" is always an explicit <c>Evaluate</c> parameter (never
/// <c>DateTime.Today</c>), so these stay deterministic regardless of when the suite actually runs.
/// Card fixture: CreditLimit = $2,000; StatementCutOffDay = 25, PaymentDueDay = 10, which yields a
/// cycle ending 2026-08-25 with due date 2026-09-10 (per CreditCard.GetPaymentDueDateForCycleEndingOn's
/// worked example in README §15). Statement fixture: MinimumPayment = $50, PayInFullAmount = $300.
/// </summary>
public sealed class CreditCardHealthEvaluatorTests
{
    private static readonly DateOnly CycleEndDate = new(2026, 8, 25);
    private static readonly DateOnly DueDate = new(2026, 9, 10);

    private static CreditCard CreateCard(decimal amountOwed) => new(
        name: "Test Card",
        currency: CurrencyCode.USD,
        issuer: "Test Bank",
        creditLimit: 2000m,
        statementCutOffDay: 25,
        paymentDueDay: 10,
        openingAmountOwed: amountOwed);

    private static CreditCardStatement CreateStatement(CreditCard card) => new(
        card.Id,
        cycleStartDate: new DateOnly(2026, 7, 26),
        cycleEndDate: CycleEndDate,
        minimumPayment: 50m,
        payInFullAmount: 300m);

    [Fact]
    public void Evaluate_PaidInFullBeforeDueDate_IsGreen()
    {
        var card = CreateCard(amountOwed: 300m);
        var statement = CreateStatement(card);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 300m, today: new DateOnly(2026, 9, 5));

        Assert.Equal(CreditCardHealthStatus.Green, assessment.Status);
    }

    [Fact]
    public void Evaluate_NotPaidInFullNotOverdueDueSoon_IsYellow()
    {
        var card = CreateCard(amountOwed: 650m);
        var statement = CreateStatement(card);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 0m, today: new DateOnly(2026, 9, 8));

        Assert.Equal(CreditCardHealthStatus.Yellow, assessment.Status);
    }

    [Fact]
    public void Evaluate_PartialPaymentExactlyMinimum_IsOrange()
    {
        var card = CreateCard(amountOwed: 650m);
        var statement = CreateStatement(card);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 50m, today: new DateOnly(2026, 9, 8));

        Assert.Equal(CreditCardHealthStatus.Orange, assessment.Status);
    }

    [Fact]
    public void Evaluate_HighUtilization_IsOrange()
    {
        var card = CreateCard(amountOwed: 1700m); // 85% of $2,000
        var statement = CreateStatement(card);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 300m, today: new DateOnly(2026, 9, 5));

        Assert.Equal(CreditCardHealthStatus.Orange, assessment.Status);
    }

    [Fact]
    public void Evaluate_OverdueAndMinimumNotMet_IsRed()
    {
        var card = CreateCard(amountOwed: 650m);
        var statement = CreateStatement(card);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 0m, today: new DateOnly(2026, 9, 15));

        Assert.Equal(CreditCardHealthStatus.Red, assessment.Status);
    }

    [Fact]
    public void Evaluate_OverdueWithZeroMinimumAndUnpaidBalance_IsRed()
    {
        // Regression guard for the checkpoint review finding: a $0 recorded minimum must not
        // trivially satisfy "paid at least the minimum" (paymentsMadeThisCycle >= 0 is always true).
        // An overdue card with a $0 minimum and its entire balance still unpaid must be Red, exactly
        // like a card with a nonzero minimum that wasn't met — not fall through to Green.
        var card = CreateCard(amountOwed: 650m);
        var statement = new CreditCardStatement(card.Id, cycleStartDate: new DateOnly(2026, 7, 26), cycleEndDate: CycleEndDate, minimumPayment: 0m, payInFullAmount: 300m);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 0m, today: new DateOnly(2026, 9, 15));

        Assert.Equal(CreditCardHealthStatus.Red, assessment.Status);
    }

    [Fact]
    public void Evaluate_ZeroBalanceWithZeroMinimum_IsStillGreen_NotFalselyRed()
    {
        // The flip side of the regression above: a genuinely paid-off card ($0 owed) with a $0
        // recorded minimum must NOT become falsely Red — the fix only tightens what "met" means when
        // there's still a balance owed, it must not penalize a card that owes nothing at all.
        var card = CreateCard(amountOwed: 0m);
        var statement = new CreditCardStatement(card.Id, cycleStartDate: new DateOnly(2026, 7, 26), cycleEndDate: CycleEndDate, minimumPayment: 0m, payInFullAmount: 0m);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 0m, today: new DateOnly(2026, 9, 5));

        Assert.Equal(CreditCardHealthStatus.Green, assessment.Status);
    }

    [Fact]
    public void Evaluate_OverdueButMinimumPaid_FallsThroughToOrangeNotRed()
    {
        // The most important boundary case: carrying a revolving balance past the due date after
        // paying at least the minimum is normal and must NOT be flagged Red — only the pay-in-full
        // amount, not the minimum, is what's missing here.
        var card = CreateCard(amountOwed: 650m);
        var statement = CreateStatement(card);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 50m, today: new DateOnly(2026, 9, 15));

        Assert.Equal(CreditCardHealthStatus.Orange, assessment.Status);
        Assert.NotEqual(CreditCardHealthStatus.Red, assessment.Status);
    }

    [Fact]
    public void Evaluate_NoStatementYet_ShortCircuitsToNoStatementYet()
    {
        var card = CreateCard(amountOwed: 0m);

        var assessment = new CreditCardHealthEvaluator().Evaluate(card, latestStatement: null, paymentsMadeThisCycle: 0m, today: new DateOnly(2026, 9, 5));

        Assert.Equal(CreditCardHealthStatus.NoStatementYet, assessment.Status);
        Assert.Null(assessment.DueDate);
        Assert.Null(assessment.MinimumPayment);
        Assert.Null(assessment.PayInFullAmount);
        Assert.Equal(0m, assessment.PaymentsMadeThisCycle);
    }

    [Fact]
    public void Evaluate_ResultsAreDeterministic_RegardlessOfWhenTheTestRuns()
    {
        // "today" is always an explicit parameter, never read from the system clock inside the
        // evaluator, so the same inputs produce the same result no matter the actual run date.
        var card = CreateCard(amountOwed: 650m);
        var statement = CreateStatement(card);

        var first = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 50m, today: new DateOnly(2026, 9, 15));
        var second = new CreditCardHealthEvaluator().Evaluate(card, statement, paymentsMadeThisCycle: 50m, today: new DateOnly(2026, 9, 15));

        Assert.Equal(first, second);
        Assert.Equal(DueDate, first.DueDate);
    }
}
