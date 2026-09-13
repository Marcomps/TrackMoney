using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Domain.Tests.Transactions;

public sealed class InvestmentWithdrawalTests
{
    [Fact]
    public void Constructor_WithSameSourceAndDestination_Throws()
    {
        var accountId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new InvestmentWithdrawal(DateOnly.FromDateTime(DateTime.Today), 100m, accountId, accountId));
    }

    [Fact]
    public void Constructor_WithDifferentAccounts_SetsSourceAndDestination()
    {
        var fundId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();

        var withdrawal = new InvestmentWithdrawal(DateOnly.FromDateTime(DateTime.Today), 100m, fundId, destinationId, "Cash out", "note");

        Assert.Equal(fundId, withdrawal.SourceAccountId);
        Assert.Equal(destinationId, withdrawal.DestinationAccountId);
        Assert.Equal(100m, withdrawal.Amount);
        Assert.Equal("Cash out", withdrawal.Description);
        Assert.Equal("note", withdrawal.Notes);
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvestmentWithdrawal(DateOnly.FromDateTime(DateTime.Today), 0m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void NeverCountsAsSpendOrIncome()
    {
        // README §9.1 / CLAUDE.md: moving money out of an investment fund is neither spend nor income.
        var withdrawal = new InvestmentWithdrawal(DateOnly.FromDateTime(DateTime.Today), 100m, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(withdrawal.CountsAsExpense);
        Assert.False(withdrawal.CountsAsIncome);
        Assert.Null(withdrawal.SpendCategoryId);
        Assert.Null(withdrawal.SpendAccountId);
        Assert.Null(withdrawal.IncomeAccountId);
    }
}
