using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Tests.CreditAccounts;

public sealed class LoanTests
{
    private static Loan CreateLoan(
        decimal originalAmount = 5000m,
        decimal currentBalance = 3250m,
        decimal interestRate = 0.12m,
        decimal monthlyInstallment = 150m,
        decimal requiredPayment = 150m,
        decimal? fees = null,
        DateOnly? nextPaymentDate = null) =>
        new(
            "Car Loan",
            CurrencyCode.USD,
            "Bank of Example",
            LoanKind.AutoLoan,
            originalAmount,
            currentBalance,
            interestRate,
            LoanRateType.Fixed,
            monthlyInstallment,
            nextPaymentDate ?? new DateOnly(2026, 10, 1),
            requiredPayment,
            fees);

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Loan(
                " ",
                CurrencyCode.USD,
                "Bank of Example",
                LoanKind.AutoLoan,
                5000m,
                3250m,
                0.12m,
                LoanRateType.Fixed,
                150m,
                new DateOnly(2026, 10, 1),
                150m));
    }

    [Fact]
    public void Constructor_WithEmptyInstitution_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Loan(
                "Car Loan",
                CurrencyCode.USD,
                " ",
                LoanKind.AutoLoan,
                5000m,
                3250m,
                0.12m,
                LoanRateType.Fixed,
                150m,
                new DateOnly(2026, 10, 1),
                150m));
    }

    [Fact]
    public void Constructor_WithNonPositiveOriginalAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLoan(originalAmount: 0m));
    }

    [Fact]
    public void Constructor_WithNegativeCurrentBalance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLoan(currentBalance: -1m));
    }

    [Fact]
    public void Constructor_WithNegativeInterestRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLoan(interestRate: -0.01m));
    }

    [Fact]
    public void Constructor_WithNonPositiveMonthlyInstallment_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLoan(monthlyInstallment: 0m));
    }

    [Fact]
    public void Constructor_WithNonPositiveRequiredPayment_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLoan(requiredPayment: 0m));
    }

    [Fact]
    public void Constructor_WithNegativeFees_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLoan(fees: -1m));
    }

    [Fact]
    public void RemainingPayments_ComputesCeilingOfBalanceOverInstallment()
    {
        var loan = CreateLoan(currentBalance: 3250m, monthlyInstallment: 150m);

        Assert.Equal(22, loan.RemainingPayments);
    }

    [Fact]
    public void RegisterPayment_ReducesAmountOwed()
    {
        var loan = CreateLoan(currentBalance: 3250m);

        loan.RegisterPayment(150m);

        Assert.Equal(3100m, loan.AmountOwed);
    }

    [Fact]
    public void RegisterPayment_ExceedingAmountOwed_Throws()
    {
        var loan = CreateLoan(currentBalance: 200m);

        Assert.Throws<InvalidOperationException>(() => loan.RegisterPayment(200.01m));
    }

    [Fact]
    public void AdvanceSchedule_UpdatesNextPaymentDateAndRequiredPayment_ToExactlyTheSuppliedValues()
    {
        var loan = CreateLoan(nextPaymentDate: new DateOnly(2026, 10, 1), requiredPayment: 150m);
        var nextPaymentDate = new DateOnly(2026, 11, 1);

        loan.AdvanceSchedule(nextPaymentDate, 175m);

        Assert.Equal(nextPaymentDate, loan.NextPaymentDate);
        Assert.Equal(175m, loan.RequiredPayment);
    }

    [Fact]
    public void AdvanceSchedule_WithNonPositiveRequiredPayment_Throws()
    {
        var loan = CreateLoan();

        Assert.Throws<ArgumentOutOfRangeException>(() => loan.AdvanceSchedule(new DateOnly(2026, 11, 1), 0m));
    }

    [Fact]
    public void AdvanceSchedule_DoesNotMutateAmountOwed()
    {
        var loan = CreateLoan(currentBalance: 3250m);

        loan.AdvanceSchedule(new DateOnly(2026, 11, 1), 175m);

        Assert.Equal(3250m, loan.AmountOwed);
    }
}
