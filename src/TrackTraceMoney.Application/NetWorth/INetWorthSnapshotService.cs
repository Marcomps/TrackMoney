using TrackTraceMoney.Application.Reporting;

namespace TrackTraceMoney.Application.NetWorth;

public interface INetWorthSnapshotService
{
    /// <summary>Recomputes net worth from currently active accounts and upserts one
    /// <see cref="Domain.NetWorth.NetWorthSnapshot"/> row per currency for <paramref name="asOfDate"/>
    /// (README §24). Called as a side effect of every Dashboard load, not from a manual button —
    /// this is what advances the net worth evolution timeline.</summary>
    Task RecordSnapshotAsync(DateOnly asOfDate, CancellationToken ct = default);

    /// <summary>
    /// Same upsert behavior as <see cref="RecordSnapshotAsync(DateOnly, CancellationToken)"/>, but skips
    /// the internal active-account fetch and <see cref="INetWorthCalculator"/> call when the caller has
    /// already computed a <see cref="NetWorthSummary"/> for the same accounts (e.g. <c>DashboardViewModel</c>,
    /// which needs the summary for its own net worth tile anyway) — avoids a redundant round-trip and
    /// recalculation on every Dashboard load.
    /// </summary>
    Task RecordSnapshotAsync(DateOnly asOfDate, NetWorthSummary summary, CancellationToken ct = default);
}
