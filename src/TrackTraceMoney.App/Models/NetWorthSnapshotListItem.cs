using TrackTraceMoney.Domain.NetWorth;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in a single currency's net worth evolution history (README §24).</summary>
public sealed record NetWorthSnapshotListItem(DateOnly AsOfDate, decimal TotalAssets, decimal TotalLiabilities, decimal NetWorth)
{
    public static NetWorthSnapshotListItem FromDomain(NetWorthSnapshot snapshot) =>
        new(snapshot.AsOfDate, snapshot.TotalAssets, snapshot.TotalLiabilities, snapshot.NetWorth);
}
