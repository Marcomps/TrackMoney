using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    public CategoryRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Category>>(async () => await Context.Set<Category>().Where(c => c.IsActive).ToListAsync(ct), ct);
}
