using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class RecurringExpenseRepository : RepositoryBase<RecurringExpense>, IRecurringExpenseRepository
{
    public RecurringExpenseRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<RecurringExpense>>(async () => await Context.Set<RecurringExpense>().Where(r => r.IsActive).ToListAsync(ct), ct);
}
