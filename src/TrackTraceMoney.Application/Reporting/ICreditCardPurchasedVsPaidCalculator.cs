using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

public interface ICreditCardPurchasedVsPaidCalculator
{
    /// <param name="purchases">CreditCardPurchase transactions for the card, already filtered to the window.</param>
    /// <param name="payments">CreditCardPayment transactions for the card, already filtered to the window.</param>
    PurchasedVsPaidResult Calculate(
        Guid creditAccountId,
        CurrencyCode currency,
        DateOnly windowStart,
        DateOnly windowEnd,
        IEnumerable<Transaction> purchases,
        IEnumerable<Transaction> payments);
}
