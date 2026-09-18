using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Aggregates spend by calendar month (README §40 "Monthly expenses trend" report). Named
/// <c>Monthly...</c> deliberately, not a <c>Granularity</c> enum with one implemented case — daily/weekly
/// are a deferred, not-yet-designed extension (see the Reports slice 1 spec), so extending this later is
/// a small explicit decision rather than a premature abstraction now.
/// </summary>
public interface IMonthlySpendingTrendCalculator
{
    /// <param name="transactions">Transactions to aggregate, typically spanning multiple months.</param>
    /// <param name="accountCurrencies">
    /// Maps a <c>FinancialAccount</c>/<c>CreditAccount.Id</c> to its <c>Currency</c> — needed because
    /// <see cref="Transaction"/> itself has no currency, only the account it moves money through does.
    /// </param>
    MonthlySpendingTrendSummary Calculate(
        IEnumerable<Transaction> transactions,
        IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies);
}
