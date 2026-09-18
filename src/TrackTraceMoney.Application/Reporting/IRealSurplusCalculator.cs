using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

public interface IRealSurplusCalculator
{
    /// <summary>
    /// Aggregates README §35's real surplus per currency. A dumb aggregator, same design stance as
    /// <see cref="INetWorthCalculator"/>: it does not filter or gather — all three inputs are already
    /// currency-keyed totals the caller (<c>DashboardViewModel</c>) computed by applying the real
    /// domain filtering rules (recurring-expense due-date + currency-map lookup; loan due-date filter;
    /// card statement/payment/due-date lookup) that belong colocated with the rest of the Dashboard's
    /// gather-and-filter code, not hidden inside this calculator.
    /// </summary>
    RealSurplusSummary Calculate(
        IReadOnlyDictionary<CurrencyCode, decimal> availableBalanceByCurrency,
        IReadOnlyDictionary<CurrencyCode, decimal> upcomingExpensesByCurrency,
        IReadOnlyDictionary<CurrencyCode, decimal> debtPaymentsByCurrency);
}
