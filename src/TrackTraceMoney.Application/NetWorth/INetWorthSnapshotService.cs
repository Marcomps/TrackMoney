namespace TrackTraceMoney.Application.NetWorth;

public interface INetWorthSnapshotService
{
    /// <summary>Recomputes net worth from currently active accounts and upserts one
    /// <see cref="Domain.NetWorth.NetWorthSnapshot"/> row per currency for <paramref name="asOfDate"/>
    /// (README §24). Called as a side effect of every Dashboard load, not from a manual button —
    /// this is what advances the net worth evolution timeline.</summary>
    Task RecordSnapshotAsync(DateOnly asOfDate, CancellationToken ct = default);
}
