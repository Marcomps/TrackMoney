using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.TransactionPresets;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class TransactionPresetRepository : RepositoryBase<TransactionPreset>, ITransactionPresetRepository
{
    public TransactionPresetRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<TransactionPreset>> GetActiveAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<TransactionPreset>>(async () => await Context.Set<TransactionPreset>().Where(p => p.IsActive).ToListAsync(ct), ct);
}
