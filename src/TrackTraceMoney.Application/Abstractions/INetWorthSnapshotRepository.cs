using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.NetWorth;

namespace TrackTraceMoney.Application.Abstractions;

public interface INetWorthSnapshotRepository : IRepository<NetWorthSnapshot>
{
    Task<NetWorthSnapshot?> GetByCurrencyAndDateAsync(CurrencyCode currency, DateOnly asOfDate, CancellationToken ct = default);

    /// <summary>Ordered by <see cref="NetWorthSnapshot.AsOfDate"/> ascending (oldest-first) — the query
    /// itself does the ordering so callers (e.g. the Net Worth history screen) never have to.</summary>
    Task<IReadOnlyList<NetWorthSnapshot>> GetForCurrencyAsync(CurrencyCode currency, CancellationToken ct = default);
}
