using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Computes README §35's real surplus formula: available balance minus upcoming recurring expenses
/// minus debt payments, per currency, from three pre-filtered currency-keyed totals the caller already
/// gathered (see <see cref="IRealSurplusCalculator"/>'s remarks on why this stays a dumb aggregator).
/// No floor at zero anywhere — a currency with obligations but insufficient/no balance is a legitimate,
/// valuable negative-surplus signal to surface (mirrors <see cref="NetWorthCalculator"/>'s own
/// no-floor precedent for a negative net worth).
/// </summary>
public sealed class RealSurplusCalculator : IRealSurplusCalculator
{
    public RealSurplusSummary Calculate(
        IReadOnlyDictionary<CurrencyCode, decimal> availableBalanceByCurrency,
        IReadOnlyDictionary<CurrencyCode, decimal> upcomingExpensesByCurrency,
        IReadOnlyDictionary<CurrencyCode, decimal> debtPaymentsByCurrency)
    {
        var currencies = availableBalanceByCurrency.Keys
            .Union(upcomingExpensesByCurrency.Keys)
            .Union(debtPaymentsByCurrency.Keys);

        var byCurrency = currencies.ToDictionary(
            currency => currency,
            currency => RealSurplus.Calculate(
                currency,
                availableBalanceByCurrency.GetValueOrDefault(currency),
                upcomingExpensesByCurrency.GetValueOrDefault(currency),
                debtPaymentsByCurrency.GetValueOrDefault(currency)));

        return new RealSurplusSummary { ByCurrency = byCurrency };
    }
}
