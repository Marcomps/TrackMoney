using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class BudgetRepository : RepositoryBase<Budget>, IBudgetRepository
{
    public BudgetRepository(TrackTraceMoneyDbContext context) : base(context)
    {
    }

    public async Task<Budget?> GetForCategoryAndMonthAsync(Guid categoryId, int year, int month, CancellationToken ct = default) =>
        await Context.Set<Budget>()
            .FirstOrDefaultAsync(b => b.CategoryId == categoryId && b.Year == year && b.Month == month, ct);

    public async Task<IReadOnlyList<Budget>> GetForMonthAsync(int year, int month, CancellationToken ct = default) =>
        await Context.Set<Budget>()
            .Where(b => b.Year == year && b.Month == month)
            .ToListAsync(ct);
}
