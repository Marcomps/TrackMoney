using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class InvestmentValuationRepository : RepositoryBase<InvestmentValuation>, IInvestmentValuationRepository
{
    public InvestmentValuationRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<InvestmentValuation>> GetForFundAsync(Guid investmentFundId, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<InvestmentValuation>>(async () => await Context.Set<InvestmentValuation>()
            .Where(v => v.InvestmentFundId == investmentFundId)
            .OrderByDescending(v => v.AsOfDate)
            .ToListAsync(ct), ct);
}
