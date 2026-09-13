using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.NetWorth;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class NetWorthSnapshotRepository : RepositoryBase<NetWorthSnapshot>, INetWorthSnapshotRepository
{
    public NetWorthSnapshotRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<NetWorthSnapshot?> GetByCurrencyAndDateAsync(CurrencyCode currency, DateOnly asOfDate, CancellationToken ct = default) =>
        GuardedAsync(async () => await Context.Set<NetWorthSnapshot>()
            .FirstOrDefaultAsync(s => s.Currency == currency && s.AsOfDate == asOfDate, ct), ct);

    public Task<IReadOnlyList<NetWorthSnapshot>> GetForCurrencyAsync(CurrencyCode currency, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<NetWorthSnapshot>>(async () => await Context.Set<NetWorthSnapshot>()
            .Where(s => s.Currency == currency)
            .OrderBy(s => s.AsOfDate)
            .ToListAsync(ct), ct);
}
