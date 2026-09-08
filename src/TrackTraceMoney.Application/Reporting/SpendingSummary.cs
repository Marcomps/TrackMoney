using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Spend aggregated per currency (never blended across currencies — README §6/§10, CLAUDE.md).
/// A $20 USD expense and a $20 MXN expense in the same category must never sum to "40"; every total
/// exposed here is keyed by <see cref="CurrencyCode"/> so callers can't accidentally add them together.
/// </summary>
public sealed class SpendingSummary
{
    public IReadOnlyDictionary<CurrencyCode, decimal> TotalSpentByCurrency { get; init; } = new Dictionary<CurrencyCode, decimal>();

    public IReadOnlyDictionary<(CurrencyCode Currency, Guid CategoryId), decimal> SpentByCategoryAndCurrency { get; init; } =
        new Dictionary<(CurrencyCode, Guid), decimal>();

    public decimal GetSpentForCategory(Guid categoryId, CurrencyCode currency) =>
        SpentByCategoryAndCurrency.GetValueOrDefault((currency, categoryId));
}
