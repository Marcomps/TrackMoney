using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Spend aggregated per currency, per calendar month (never blended across currencies — README §6/§10,
/// CLAUDE.md). Keyed by <c>(Currency, Year, Month)</c> rather than a single date so callers never
/// accidentally sum two different currencies' amounts for the "same" month.
/// </summary>
public sealed class MonthlySpendingTrendSummary
{
    public IReadOnlyDictionary<(CurrencyCode Currency, int Year, int Month), decimal> SpentByCurrencyAndMonth { get; init; } =
        new Dictionary<(CurrencyCode, int, int), decimal>();
}
