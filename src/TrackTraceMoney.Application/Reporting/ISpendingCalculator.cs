using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

public interface ISpendingCalculator
{
    /// <param name="transactions">Transactions to aggregate.</param>
    /// <param name="accountCurrencies">
    /// Maps a <c>FinancialAccount.Id</c> to its <c>Currency</c> — needed because <see cref="Transaction"/>
    /// itself has no currency, only the account it moves money through does.
    /// </param>
    SpendingSummary Calculate(IEnumerable<Transaction> transactions, IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies);
}
