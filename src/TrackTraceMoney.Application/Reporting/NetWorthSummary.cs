using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Net worth aggregated per currency (never blended across currencies — CLAUDE.md hard constraint,
/// README §24). Mirrors <see cref="SpendingSummary"/>'s currency-keyed stance so callers can't
/// accidentally add a USD figure to an MXN one.
/// </summary>
public sealed class NetWorthSummary
{
    public IReadOnlyDictionary<CurrencyCode, NetWorthByCurrency> ByCurrency { get; init; } = new Dictionary<CurrencyCode, NetWorthByCurrency>();
}

public sealed record NetWorthByCurrency(CurrencyCode Currency, decimal TotalAssets, decimal TotalLiabilities)
{
    public decimal NetWorth => TotalAssets - TotalLiabilities;
}
