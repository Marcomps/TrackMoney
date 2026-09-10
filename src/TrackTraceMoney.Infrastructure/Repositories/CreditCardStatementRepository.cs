using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class CreditCardStatementRepository : RepositoryBase<CreditCardStatement>, ICreditCardStatementRepository
{
    public CreditCardStatementRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<CreditCardStatement?> GetLatestForCardAsync(Guid creditAccountId, CancellationToken ct = default) =>
        GuardedAsync(() => Context.Set<CreditCardStatement>()
            .Where(s => s.CreditAccountId == creditAccountId)
            .OrderByDescending(s => s.CycleEndDate)
            .FirstOrDefaultAsync(ct), ct);

    public Task<IReadOnlyList<CreditCardStatement>> GetForCardAsync(Guid creditAccountId, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<CreditCardStatement>>(async () => await Context.Set<CreditCardStatement>()
            .Where(s => s.CreditAccountId == creditAccountId)
            .OrderByDescending(s => s.CycleEndDate)
            .ToListAsync(ct), ct);

    public Task<IReadOnlyDictionary<Guid, CreditCardStatement>> GetLatestForCardsAsync(IEnumerable<Guid> creditAccountIds, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyDictionary<Guid, CreditCardStatement>>(async () =>
        {
            var ids = creditAccountIds.ToList();
            var statements = await Context.Set<CreditCardStatement>()
                .Where(s => ids.Contains(s.CreditAccountId))
                .ToListAsync(ct);
            return statements
                .GroupBy(s => s.CreditAccountId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CycleEndDate).First());
        }, ct);
}
