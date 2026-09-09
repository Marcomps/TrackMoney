using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class CreditAccountRepository : RepositoryBase<CreditAccount>, ICreditAccountRepository
{
    public CreditAccountRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<CreditAccount>> GetActiveAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<CreditAccount>>(async () => await Context.Set<CreditAccount>().Where(a => a.IsActive).ToListAsync(ct), ct);
}
