using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.Reporting;

public interface INetWorthCalculator
{
    /// <summary>
    /// Aggregates net worth per currency. A dumb aggregator, same design stance as
    /// <see cref="ISpendingCalculator"/>: it does not filter by <c>IsActive</c> — the caller decides
    /// which accounts to hand it (see <see cref="TrackTraceMoney.Application.NetWorth.INetWorthSnapshotService"/>,
    /// which passes only active accounts).
    /// </summary>
    NetWorthSummary Calculate(IEnumerable<FinancialAccount> financialAccounts, IEnumerable<CreditAccount> creditAccounts);
}
