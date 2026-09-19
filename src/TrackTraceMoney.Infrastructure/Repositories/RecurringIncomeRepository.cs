using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.RecurringIncomes;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class RecurringIncomeRepository : RepositoryBase<RecurringIncome>, IRecurringIncomeRepository
{
    public RecurringIncomeRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<RecurringIncome>> GetActiveAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<RecurringIncome>>(async () => await Context.Set<RecurringIncome>().Where(r => r.IsActive).ToListAsync(ct), ct);

    public Task<bool> HasAnyReferencingAccountAsync(Guid accountId, CancellationToken ct = default) =>
        GuardedAsync(() => Context.Set<RecurringIncome>().AnyAsync(r => r.DestinationAccountId == accountId, ct), ct);
}
