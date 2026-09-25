using TrackTraceMoney.Application.CreditAccounts;

namespace TrackTraceMoney.Application.Tests.CreditAccounts;

public sealed class SnowballPayoffEstimatorTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SnowballDebtInput Debt(string name, decimal owed, decimal minimum, int order = 0) =>
        new(Guid.NewGuid(), name, owed, minimum, Created.AddMinutes(order));

    [Fact]
    public void SingleDebt_MinimumOnly_PaysOffInCeilingOfBalanceOverMinimum()
    {
        var loan = Debt("Loan", 1000m, 150m);

        var result = SnowballPayoffEstimator.EstimatePayoffMonths([loan], 0m);

        Assert.Equal(7, result[loan.CreditAccountId]); // 6 × 150 = 900, month 7 pays the last 100
    }

    [Fact]
    public void Extra_GoesToSmallestDebt_AndItsMinimumRollsIntoTheNextOne()
    {
        var small = Debt("Card", 300m, 50m);
        var big = Debt("Loan", 1000m, 100m);

        // Month 1: card 300-50-100=150, loan 900. Month 2: card paid (150-50-100), loan 800.
        // Month 3+: loan gets 100 + 50 (freed card minimum) + 100 extra = 250/month -> 800 in 4 months.
        var result = SnowballPayoffEstimator.EstimatePayoffMonths([big, small], 100m);

        Assert.Equal(2, result[small.CreditAccountId]);
        Assert.Equal(6, result[big.CreditAccountId]);
    }

    [Fact]
    public void Extra_ShortensTotalTime_ComparedToMinimumsOnly()
    {
        var debts = new[] { Debt("A", 2000m, 100m), Debt("B", 500m, 40m, 1) };

        var withoutExtra = SnowballPayoffEstimator.EstimatePayoffMonths(debts, 0m).Values.Max();
        var withExtra = SnowballPayoffEstimator.EstimatePayoffMonths(debts, 200m).Values.Max();

        Assert.True(withExtra < withoutExtra);
    }

    [Fact]
    public void ZeroMinimum_AndNoExtra_IsNeverPaidOff()
    {
        var card = Debt("Card with no statement minimum", 500m, 0m);

        var result = SnowballPayoffEstimator.EstimatePayoffMonths([card], 0m);

        Assert.Null(result[card.CreditAccountId]);
    }

    [Fact]
    public void PaidOffDebts_AreLeftOut()
    {
        var done = Debt("Done", 0m, 50m);

        Assert.Empty(SnowballPayoffEstimator.EstimatePayoffMonths([done], 0m));
    }
}
