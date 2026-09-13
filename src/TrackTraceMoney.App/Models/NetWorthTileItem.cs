using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Net worth for a single currency, shown on the Dashboard's Net Worth tile (README §24). Mirrors
/// <see cref="CurrencyBalance"/>'s stance — currencies are never summed/converted together.
/// </summary>
public sealed record NetWorthTileItem(CurrencyCode Currency, decimal TotalAssets, decimal TotalLiabilities, decimal NetWorth)
{
    public static NetWorthTileItem FromDomain(NetWorthByCurrency byCurrency) =>
        new(byCurrency.Currency, byCurrency.TotalAssets, byCurrency.TotalLiabilities, byCurrency.NetWorth);
}
