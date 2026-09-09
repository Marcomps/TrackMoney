using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Reporting;

/// <summary>
/// README §17 "purchased vs. paid" — worked numeric examples supplied by the product-owner scoping
/// pass for this slice. "Today" is fixed at 2026-09-09 throughout (never <c>DateTime.Today</c>), so
/// these stay deterministic regardless of when the suite actually runs.
/// </summary>
public sealed class CreditCardPurchasedVsPaidCalculatorTests
{
    private static readonly Guid CreditAccountId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid SourceAccountId = Guid.NewGuid();

    // All purchases/payments from the worked dataset, unfiltered — each test slices this down to its
    // own window, mirroring what the repository's date-range query would hand the calculator.
    private static IReadOnlyList<CreditCardPurchase> AllPurchases { get; } =
    [
        new(new DateOnly(2026, 1, 10), 500m, CreditAccountId, CategoryId),
        new(new DateOnly(2026, 3, 15), 400m, CreditAccountId, CategoryId),
        new(new DateOnly(2026, 8, 15), 200m, CreditAccountId, CategoryId),
        new(new DateOnly(2026, 8, 28), 80m, CreditAccountId, CategoryId),
        new(new DateOnly(2026, 9, 1), 150m, CreditAccountId, CategoryId),
        new(new DateOnly(2026, 9, 5), 300m, CreditAccountId, CategoryId),
    ];

    private static IReadOnlyList<CreditCardPayment> AllPayments { get; } =
    [
        new(new DateOnly(2026, 1, 15), 500m, SourceAccountId, CreditAccountId),
        new(new DateOnly(2026, 3, 20), 400m, SourceAccountId, CreditAccountId),
        new(new DateOnly(2026, 8, 20), 100m, SourceAccountId, CreditAccountId),
        new(new DateOnly(2026, 8, 29), 50m, SourceAccountId, CreditAccountId),
        new(new DateOnly(2026, 9, 3), 300m, SourceAccountId, CreditAccountId),
    ];

    private static PurchasedVsPaidResult Calculate(DateOnly windowStart, DateOnly windowEnd)
    {
        var purchases = AllPurchases.Where(p => p.Date >= windowStart && p.Date <= windowEnd);
        var payments = AllPayments.Where(p => p.Date >= windowStart && p.Date <= windowEnd);

        return new CreditCardPurchasedVsPaidCalculator().Calculate(
            CreditAccountId, CurrencyCode.USD, windowStart, windowEnd, purchases, payments);
    }

    [Fact]
    public void Calculate_MonthWindow_MatchesWorkedExample()
    {
        var result = Calculate(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9));

        Assert.Equal(450m, result.TotalPurchased);
        Assert.Equal(300m, result.TotalPaid);
        Assert.Equal(-150m, result.Difference);
        Assert.True(result.IsSpendingMoreThanPaying);
    }

    [Fact]
    public void Calculate_ThreeMonthWindow_MatchesWorkedExample()
    {
        var result = Calculate(new DateOnly(2026, 6, 9), new DateOnly(2026, 9, 9));

        Assert.Equal(730m, result.TotalPurchased);
        Assert.Equal(450m, result.TotalPaid);
        Assert.Equal(-280m, result.Difference);
        Assert.True(result.IsSpendingMoreThanPaying);
    }

    [Fact]
    public void Calculate_SixMonthWindow_MatchesWorkedExample()
    {
        var result = Calculate(new DateOnly(2026, 3, 9), new DateOnly(2026, 9, 9));

        Assert.Equal(1130m, result.TotalPurchased);
        Assert.Equal(850m, result.TotalPaid);
        Assert.Equal(-280m, result.Difference);
        Assert.True(result.IsSpendingMoreThanPaying);
    }

    [Fact]
    public void Calculate_YearWindow_MatchesWorkedExample()
    {
        var result = Calculate(new DateOnly(2025, 9, 9), new DateOnly(2026, 9, 9));

        Assert.Equal(1630m, result.TotalPurchased);
        Assert.Equal(1350m, result.TotalPaid);
        Assert.Equal(-280m, result.Difference);
        Assert.True(result.IsSpendingMoreThanPaying);
    }

    [Fact]
    public void Calculate_GreenCase_FromSpecExample_PaysMoreThanSpent()
    {
        var purchases = new[] { new CreditCardPurchase(new DateOnly(2026, 9, 1), 650m, CreditAccountId, CategoryId) };
        var payments = new[] { new CreditCardPayment(new DateOnly(2026, 9, 2), 800m, SourceAccountId, CreditAccountId) };

        var result = new CreditCardPurchasedVsPaidCalculator().Calculate(
            CreditAccountId, CurrencyCode.USD, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9), purchases, payments);

        Assert.Equal(650m, result.TotalPurchased);
        Assert.Equal(800m, result.TotalPaid);
        Assert.Equal(150m, result.Difference);
        Assert.False(result.IsSpendingMoreThanPaying);
    }

    [Fact]
    public void Calculate_ExactTie_IsNotSpendingMoreThanPaying()
    {
        var purchases = new[] { new CreditCardPurchase(new DateOnly(2026, 9, 1), 500m, CreditAccountId, CategoryId) };
        var payments = new[] { new CreditCardPayment(new DateOnly(2026, 9, 2), 500m, SourceAccountId, CreditAccountId) };

        var result = new CreditCardPurchasedVsPaidCalculator().Calculate(
            CreditAccountId, CurrencyCode.USD, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9), purchases, payments);

        Assert.Equal(0m, result.Difference);
        Assert.False(result.IsSpendingMoreThanPaying, "A strict tie must count as green, mirroring BudgetStatus.IsOverBudget's strict-> convention.");
    }

    [Fact]
    public void Calculate_NoTransactionsInWindow_IsZeroAndNotSpendingMoreThanPaying()
    {
        var result = new CreditCardPurchasedVsPaidCalculator().Calculate(
            CreditAccountId, CurrencyCode.USD, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9),
            Array.Empty<Transaction>(), Array.Empty<Transaction>());

        Assert.Equal(0m, result.TotalPurchased);
        Assert.Equal(0m, result.TotalPaid);
        Assert.Equal(0m, result.Difference);
        Assert.False(result.IsSpendingMoreThanPaying);
    }

    [Fact]
    public void Calculate_OnlySumsWhatIsActuallyPassedInEachCollection_RegardlessOfConcreteType()
    {
        // The calculator itself has no type-filtering logic — it trusts whatever it's handed, exactly
        // like SpendingCalculator trusts CountsAsExpense. Correctness that purchases-only end up in the
        // "purchases" collection and payments-only end up in "payments" is the repository's job (see
        // TransactionRepositoryPurchasedVsPaidTests in the Infrastructure test project), not this
        // calculator's. This test proves the calculator sums purely by collection membership.
        var mixedAsPurchases = new Transaction[]
        {
            new CreditCardPurchase(new DateOnly(2026, 9, 1), 300m, CreditAccountId, CategoryId),
            new CreditCardPayment(new DateOnly(2026, 9, 2), 50m, SourceAccountId, CreditAccountId),
        };
        var mixedAsPayments = new Transaction[]
        {
            new CreditCardPayment(new DateOnly(2026, 9, 3), 400m, SourceAccountId, CreditAccountId),
            new CreditCardPurchase(new DateOnly(2026, 9, 4), 20m, CreditAccountId, CategoryId),
        };

        var result = new CreditCardPurchasedVsPaidCalculator().Calculate(
            CreditAccountId, CurrencyCode.USD, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9), mixedAsPurchases, mixedAsPayments);

        Assert.Equal(350m, result.TotalPurchased); // 300 + 50, regardless of concrete type
        Assert.Equal(420m, result.TotalPaid); // 400 + 20, regardless of concrete type
    }
}
