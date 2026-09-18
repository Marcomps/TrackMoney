using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Real surplus for a single currency, shown on the Dashboard's Real Surplus tile (README §35).
/// Mirrors <see cref="NetWorthTileItem"/>'s stance — currencies are never summed/converted together.
/// </summary>
public sealed record RealSurplusTileItem(CurrencyCode Currency, decimal AvailableBalance, decimal UpcomingExpenses, decimal DebtPayments, decimal Amount)
{
    public static RealSurplusTileItem FromDomain(RealSurplus surplus) =>
        new(surplus.Currency, surplus.AvailableBalance, surplus.UpcomingExpenses, surplus.DebtPayments, surplus.Amount);
}
