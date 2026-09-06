using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal abstract class RepositoryBase<TEntity> : IRepository<TEntity> where TEntity : Entity
{
    protected readonly TrackTraceMoneyDbContext Context;

    protected RepositoryBase(TrackTraceMoneyDbContext context)
    {
        Context = context;
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Context.Set<TEntity>().FindAsync([id], ct);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        await Context.Set<TEntity>().ToListAsync(ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await Context.Set<TEntity>().AddAsync(entity, ct);

    public void Remove(TEntity entity) => Context.Set<TEntity>().Remove(entity);

    public Task SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
}
