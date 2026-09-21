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

    public Task<bool> HasAnyReferencingAccountAsync(Guid accountOrCreditAccountId, CancellationToken ct = default) =>
        GuardedAsync(() => Context.Set<RecurringExpense>()
            .AnyAsync(r => r.AccountId == accountOrCreditAccountId || r.CreditAccountId == accountOrCreditAccountId, ct), ct);

    public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
        GuardedAsync(() => Context.Set<RecurringExpense>().AnyAsync(r => r.CategoryId == categoryId, ct), ct);
}
