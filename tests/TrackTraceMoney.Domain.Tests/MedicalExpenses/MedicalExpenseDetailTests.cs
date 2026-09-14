using TrackTraceMoney.Domain.MedicalExpenses;

namespace TrackTraceMoney.Domain.Tests.MedicalExpenses;

public sealed class MedicalExpenseDetailTests
{
    [Fact]
    public void Constructor_WithEmptyTransactionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new MedicalExpenseDetail(Guid.Empty, null, null, null, MedicalReimbursementStatus.None));
    }

    [Theory]
    [InlineData(MedicalReimbursementStatus.Reimbursed)]
    [InlineData(MedicalReimbursementStatus.Rejected)]
    public void Constructor_WithReimbursedOrRejectedInitialStatus_Throws(MedicalReimbursementStatus status)
    {
        Assert.Throws<ArgumentException>(() =>
            new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 50m, status));
    }

    [Fact]
    public void Constructor_StatusNone_WithNonNullGrossAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new MedicalExpenseDetail(Guid.NewGuid(), null, 100m, null, MedicalReimbursementStatus.None));
    }

    [Fact]
    public void Constructor_StatusNone_WithNonNullInsuranceCoveredAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new MedicalExpenseDetail(Guid.NewGuid(), null, null, 50m, MedicalReimbursementStatus.None));
    }

    [Theory]
    [InlineData(MedicalReimbursementStatus.Pending)]
    [InlineData(MedicalReimbursementStatus.PaidDirectly)]
    public void Constructor_InsuranceInvolved_WithNullGrossAmount_Throws(MedicalReimbursementStatus status)
    {
        Assert.Throws<ArgumentException>(() =>
            new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", null, 50m, status));
    }

    [Theory]
    [InlineData(MedicalReimbursementStatus.Pending)]
    [InlineData(MedicalReimbursementStatus.PaidDirectly)]
    public void Constructor_InsuranceInvolved_WithNonPositiveGrossAmount_Throws(MedicalReimbursementStatus status)
    {
        Assert.Throws<ArgumentException>(() =>
            new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 0m, 50m, status));
    }

    [Fact]
    public void Constructor_InsuranceCoveredAmountExceedsGrossAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 150m, MedicalReimbursementStatus.Pending));
    }

    [Fact]
    public void Constructor_StatusNone_WithNullAmounts_Succeeds()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), null, null, null, MedicalReimbursementStatus.None);

        Assert.Equal(MedicalReimbursementStatus.None, detail.Status);
        Assert.Null(detail.GrossAmount);
        Assert.Null(detail.InsuranceCoveredAmount);
    }

    [Fact]
    public void Constructor_PendingWithValidAmounts_Succeeds()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending);

        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
        Assert.Equal(100m, detail.GrossAmount);
        Assert.Equal(40m, detail.InsuranceCoveredAmount);
    }

    [Fact]
    public void MarkReimbursed_WhenNotPending_Throws()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), null, null, null, MedicalReimbursementStatus.None);

        Assert.Throws<InvalidOperationException>(() => detail.MarkReimbursed(50m));
    }

    [Fact]
    public void MarkReimbursed_WithNonPositiveAmount_Throws()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending);

        Assert.Throws<ArgumentOutOfRangeException>(() => detail.MarkReimbursed(0m));
    }

    [Fact]
    public void MarkReimbursed_AmountExceedsGrossAmount_Throws()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending);

        Assert.Throws<ArgumentOutOfRangeException>(() => detail.MarkReimbursed(150m));
    }

    [Fact]
    public void MarkReimbursed_SuccessPath_SetsStatusAndOverwritesInsuranceCoveredAmount()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending);

        detail.MarkReimbursed(85m);

        Assert.Equal(MedicalReimbursementStatus.Reimbursed, detail.Status);
        Assert.Equal(85m, detail.InsuranceCoveredAmount);
    }

    [Fact]
    public void MarkRejected_WhenNotPending_Throws()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), null, null, null, MedicalReimbursementStatus.None);

        Assert.Throws<InvalidOperationException>(() => detail.MarkRejected());
    }

    [Fact]
    public void MarkRejected_SuccessPath_SetsStatus()
    {
        var detail = new MedicalExpenseDetail(Guid.NewGuid(), "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending);

        detail.MarkRejected();

        Assert.Equal(MedicalReimbursementStatus.Rejected, detail.Status);
    }
}
