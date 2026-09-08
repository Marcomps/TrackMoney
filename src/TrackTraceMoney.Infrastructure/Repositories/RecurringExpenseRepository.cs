using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class RecurringExpenseRepository : RepositoryBase<RecurringExpense>, IRecurringExpenseRepository
{
    public RecurringExpenseRepository(TrackTraceMoneyDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default) =>
        await Context.Set<RecurringExpense>().Where(r => r.IsActive).ToListAsync(ct);
}
