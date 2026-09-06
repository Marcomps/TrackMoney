using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class TransactionRepository : RepositoryBase<Transaction>, ITransactionRepository
{
    public TransactionRepository(TrackTraceMoneyDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await Context.Set<Transaction>()
            .Where(t => t.Date >= from && t.Date <= to)
            .ToListAsync(ct);
}
