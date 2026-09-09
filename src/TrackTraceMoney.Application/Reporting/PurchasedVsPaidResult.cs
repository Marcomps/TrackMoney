using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// README §17 "Purchased vs. paid" comparison for a single credit card over a date window. Compares
/// raw <c>CreditCardPurchase.Amount</c> against raw <c>CreditCardPayment.Amount</c> — not
/// <c>AmountOwed</c> deltas, since <c>AmountOwed</c> is a point-in-time balance rather than a windowed
/// figure. A tie counts as "not overspending" (strict <c>&gt;</c>), mirroring
/// <c>BudgetStatus.IsOverBudget</c>'s convention.
/// </summary>
public sealed record PurchasedVsPaidResult(
    Guid CreditAccountId,
    CurrencyCode Currency,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    decimal TotalPurchased,
    decimal TotalPaid)
{
    public decimal Difference => TotalPaid - TotalPurchased;

    public bool IsSpendingMoreThanPaying => TotalPurchased > TotalPaid;
}
