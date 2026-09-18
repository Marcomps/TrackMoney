using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// README §35: "sobrante real" is available balance minus known upcoming obligations (upcoming
/// recurring expenses + debt payments) — a distinct number from raw account balance ("saldo
/// disponible"). Never substitute one for the other in dashboards or reports. Per currency, like
/// every other multi-currency figure in this app (CLAUDE.md) — mirrors <see cref="NetWorthByCurrency"/>'s
/// shape/placement in this same file.
/// </summary>
public sealed record RealSurplus(
    CurrencyCode Currency, decimal AvailableBalance, decimal UpcomingExpenses, decimal DebtPayments, decimal Amount)
{
    public static RealSurplus Calculate(CurrencyCode currency, decimal availableBalance, decimal upcomingExpenses, decimal debtPayments) =>
        new(currency, availableBalance, upcomingExpenses, debtPayments, availableBalance - upcomingExpenses - debtPayments);
}

/// <summary>
/// Real surplus aggregated per currency (never blended — CLAUDE.md hard constraint). Mirrors
/// <see cref="NetWorthSummary"/>'s currency-keyed stance.
/// </summary>
public sealed class RealSurplusSummary
{
    public IReadOnlyDictionary<CurrencyCode, RealSurplus> ByCurrency { get; init; } = new Dictionary<CurrencyCode, RealSurplus>();
}
