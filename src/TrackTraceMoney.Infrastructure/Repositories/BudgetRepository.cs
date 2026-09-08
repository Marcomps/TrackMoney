using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class BudgetRepository : RepositoryBase<Budget>, IBudgetRepository
{
    public BudgetRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<Budget?> GetForCategoryAndMonthAsync(Guid categoryId, int year, int month, CurrencyCode currency, CancellationToken ct = default) =>
        GuardedAsync(() => Context.Set<Budget>()
            .FirstOrDefaultAsync(b => b.CategoryId == categoryId && b.Year == year && b.Month == month && b.Currency == currency, ct), ct);

    public Task<IReadOnlyList<Budget>> GetForMonthAsync(int year, int month, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Budget>>(async () => await Context.Set<Budget>()
            .Where(b => b.Year == year && b.Month == month)
            .ToListAsync(ct), ct);
}
