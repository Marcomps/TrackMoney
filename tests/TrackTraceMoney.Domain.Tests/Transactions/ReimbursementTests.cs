using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Domain.Tests.Transactions;

public sealed class ReimbursementTests
{
    [Fact]
    public void Constructor_SetsFieldsCorrectly()
    {
        var destinationAccountId = Guid.NewGuid();
        var linkedTransactionId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);

        var reimbursement = new Reimbursement(date, 75m, destinationAccountId, linkedTransactionId, "Insurance reimbursement", "note");

        Assert.Equal(date, reimbursement.Date);
        Assert.Equal(75m, reimbursement.Amount);
        Assert.Equal(destinationAccountId, reimbursement.DestinationAccountId);
        Assert.Equal(linkedTransactionId, reimbursement.LinkedTransactionId);
        Assert.Equal("Insurance reimbursement", reimbursement.Description);
        Assert.Equal("note", reimbursement.Notes);
    }

    [Fact]
    public void Constructor_WithEmptyLinkedTransactionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Reimbursement(DateOnly.FromDateTime(DateTime.Today), 75m, Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Reimbursement(DateOnly.FromDateTime(DateTime.Today), 0m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void CountsAsIncome_IsTrue()
    {
        var reimbursement = new Reimbursement(DateOnly.FromDateTime(DateTime.Today), 75m, Guid.NewGuid(), Guid.NewGuid());

        Assert.True(reimbursement.CountsAsIncome);
    }

    [Fact]
    public void IncomeAccountId_EqualsDestinationAccountId()
    {
        var destinationAccountId = Guid.NewGuid();
        var reimbursement = new Reimbursement(DateOnly.FromDateTime(DateTime.Today), 75m, destinationAccountId, Guid.NewGuid());

        Assert.Equal(destinationAccountId, reimbursement.IncomeAccountId);
    }

    [Fact]
    public void NeverCountsAsSpend()
    {
        // README §9.1 / CLAUDE.md: a reimbursement is income/recovery, never spend — the original
        // expense already counted as spend and this must never double it (or invert it).
        var reimbursement = new Reimbursement(DateOnly.FromDateTime(DateTime.Today), 75m, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(reimbursement.CountsAsExpense);
        Assert.Null(reimbursement.SpendCategoryId);
        Assert.Null(reimbursement.SpendAccountId);
    }
}
