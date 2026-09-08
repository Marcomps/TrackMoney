using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    public CategoryRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }
}
