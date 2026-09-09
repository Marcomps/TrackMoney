using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class TransactionRepository : RepositoryBase<Transaction>, ITransactionRepository
{
    public TransactionRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Transaction>>(async () => await Context.Set<Transaction>()
            .Where(t => t.Date >= from && t.Date <= to)
            .ToListAsync(ct), ct);

    public Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Transaction>>(async () => (await Context.Set<Transaction>()
            .Where(t => t.Date >= from && t.Date <= to)
            .ToListAsync(ct))
            .Where(t => t.SpendCategoryId == categoryId)
            .ToList(), ct);
}
