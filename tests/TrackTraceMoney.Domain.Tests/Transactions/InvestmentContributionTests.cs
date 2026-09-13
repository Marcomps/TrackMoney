using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Domain.Tests.Transactions;

public sealed class InvestmentContributionTests
{
    [Fact]
    public void Constructor_WithSameSourceAndDestination_Throws()
    {
        var accountId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new InvestmentContribution(DateOnly.FromDateTime(DateTime.Today), 100m, accountId, accountId));
    }

    [Fact]
    public void Constructor_WithDifferentAccounts_SetsSourceAndDestination()
    {
        var sourceId = Guid.NewGuid();
        var fundId = Guid.NewGuid();

        var contribution = new InvestmentContribution(DateOnly.FromDateTime(DateTime.Today), 100m, sourceId, fundId, "Monthly", "note");

        Assert.Equal(sourceId, contribution.SourceAccountId);
        Assert.Equal(fundId, contribution.DestinationAccountId);
        Assert.Equal(100m, contribution.Amount);
        Assert.Equal("Monthly", contribution.Description);
        Assert.Equal("note", contribution.Notes);
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvestmentContribution(DateOnly.FromDateTime(DateTime.Today), 0m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void NeverCountsAsSpendOrIncome()
    {
        // README §9.1 / CLAUDE.md: moving money into an investment fund is neither spend nor income.
        var contribution = new InvestmentContribution(DateOnly.FromDateTime(DateTime.Today), 100m, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(contribution.CountsAsExpense);
        Assert.False(contribution.CountsAsIncome);
        Assert.Null(contribution.SpendCategoryId);
        Assert.Null(contribution.SpendAccountId);
        Assert.Null(contribution.IncomeAccountId);
    }
}
