using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Domain.Tests.Transactions;

public sealed class InterestIncomeTests
{
    [Fact]
    public void Constructor_SetsFieldsCorrectly()
    {
        var destinationAccountId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);

        var interestIncome = new InterestIncome(date, 41.67m, destinationAccountId, "Monthly interest", "note");

        Assert.Equal(date, interestIncome.Date);
        Assert.Equal(41.67m, interestIncome.Amount);
        Assert.Equal(destinationAccountId, interestIncome.DestinationAccountId);
        Assert.Equal("Monthly interest", interestIncome.Description);
        Assert.Equal("note", interestIncome.Notes);
    }

    [Fact]
    public void CountsAsIncome_IsTrue()
    {
        var interestIncome = new InterestIncome(DateOnly.FromDateTime(DateTime.Today), 41.67m, Guid.NewGuid());

        Assert.True(interestIncome.CountsAsIncome);
    }

    [Fact]
    public void IncomeAccountId_EqualsDestinationAccountId()
    {
        var destinationAccountId = Guid.NewGuid();
        var interestIncome = new InterestIncome(DateOnly.FromDateTime(DateTime.Today), 41.67m, destinationAccountId);

        Assert.Equal(destinationAccountId, interestIncome.IncomeAccountId);
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InterestIncome(DateOnly.FromDateTime(DateTime.Today), 0m, Guid.NewGuid()));
    }

    [Fact]
    public void NeverCountsAsSpend()
    {
        // README §9.1 / CLAUDE.md: interest income must never be counted as spend.
        var interestIncome = new InterestIncome(DateOnly.FromDateTime(DateTime.Today), 41.67m, Guid.NewGuid());

        Assert.False(interestIncome.CountsAsExpense);
        Assert.Null(interestIncome.SpendCategoryId);
        Assert.Null(interestIncome.SpendAccountId);
    }
}
