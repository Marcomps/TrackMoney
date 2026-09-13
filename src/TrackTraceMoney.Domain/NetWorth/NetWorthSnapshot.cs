using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.NetWorth;

/// <summary>
/// A point-in-time record of net worth for a single currency (README §24). Not a subtype of
/// <see cref="TrackTraceMoney.Domain.Accounts.FinancialAccount"/> or
/// <see cref="TrackTraceMoney.Domain.CreditAccounts.CreditAccount"/> — it is a derived, standalone
/// fact recorded periodically (currently: every Dashboard load), not an account. Net worth is never
/// summed/converted across currencies (CLAUDE.md hard constraint) — one snapshot row exists per
/// (Currency, AsOfDate) pair, enforced by a unique index at the Infrastructure layer.
/// </summary>
public sealed class NetWorthSnapshot : Entity
{
    public CurrencyCode Currency { get; private set; }

    public DateOnly AsOfDate { get; private set; }

    public decimal TotalAssets { get; private set; }

    public decimal TotalLiabilities { get; private set; }

    /// <summary>
    /// Always derived from <see cref="TotalAssets"/>/<see cref="TotalLiabilities"/>, never stored, so
    /// it can't drift out of sync with them (mirrors <see cref="TrackTraceMoney.Domain.Accounts.InvestmentFund.Gain"/>'s
    /// pattern). Deliberately unclamped — an overdrawn set of accounts can make net worth legitimately negative.
    /// </summary>
    public decimal NetWorth => TotalAssets - TotalLiabilities;

    public NetWorthSnapshot(CurrencyCode currency, DateOnly asOfDate, decimal totalAssets, decimal totalLiabilities)
    {
        if (totalLiabilities < 0)
            throw new ArgumentOutOfRangeException(nameof(totalLiabilities), "Total liabilities cannot be negative.");

        Currency = currency;
        AsOfDate = asOfDate;
        TotalAssets = totalAssets;
        TotalLiabilities = totalLiabilities;
    }

    private NetWorthSnapshot()
    {
    }

    /// <summary>Upserts this snapshot's totals in place (used when a snapshot already exists for the
    /// same (Currency, AsOfDate) — see <see cref="TrackTraceMoney.Application.NetWorth.INetWorthSnapshotService"/>).
    /// <see cref="TotalAssets"/> is deliberately NOT floor-guarded at 0, same as the constructor.</summary>
    public void UpdateTotals(decimal totalAssets, decimal totalLiabilities)
    {
        if (totalLiabilities < 0)
            throw new ArgumentOutOfRangeException(nameof(totalLiabilities), "Total liabilities cannot be negative.");

        TotalAssets = totalAssets;
        TotalLiabilities = totalLiabilities;
    }
}
