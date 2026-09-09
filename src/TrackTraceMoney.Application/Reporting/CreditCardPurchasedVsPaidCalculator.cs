using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// README §17 — sums card purchases and payments over a caller-resolved date window and reports the
/// difference. Unlike <see cref="SpendingCalculator"/>, no per-transaction currency lookup is needed:
/// a <c>CreditAccount</c> has a single scalar <c>Currency</c>, not per-transaction currency resolution,
/// so the caller passes the card's currency straight through.
/// </summary>
public sealed class CreditCardPurchasedVsPaidCalculator : ICreditCardPurchasedVsPaidCalculator
{
    public PurchasedVsPaidResult Calculate(
        Guid creditAccountId,
        CurrencyCode currency,
        DateOnly windowStart,
        DateOnly windowEnd,
        IEnumerable<Transaction> purchases,
        IEnumerable<Transaction> payments) =>
        new(
            creditAccountId,
            currency,
            windowStart,
            windowEnd,
            purchases.Sum(t => t.Amount),
            payments.Sum(t => t.Amount));
}
