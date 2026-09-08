using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class FinancialAccountRepository : RepositoryBase<FinancialAccount>, IFinancialAccountRepository
{
    public FinancialAccountRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<FinancialAccount>> GetActiveAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<FinancialAccount>>(async () => await Context.Set<FinancialAccount>().Where(a => a.IsActive).ToListAsync(ct), ct);
}
