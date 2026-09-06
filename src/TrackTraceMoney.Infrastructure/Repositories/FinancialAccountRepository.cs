using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class FinancialAccountRepository : RepositoryBase<FinancialAccount>, IFinancialAccountRepository
{
    public FinancialAccountRepository(TrackTraceMoneyDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<FinancialAccount>> GetActiveAsync(CancellationToken ct = default) =>
        await Context.Set<FinancialAccount>().Where(a => a.IsActive).ToListAsync(ct);
}
