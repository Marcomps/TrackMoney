using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal abstract class RepositoryBase<TEntity> : IRepository<TEntity> where TEntity : Entity
{
    protected readonly TrackTraceMoneyDbContext Context;

    private readonly IDbAccessGate _gate;

    protected RepositoryBase(TrackTraceMoneyDbContext context, IDbAccessGate gate)
    {
        Context = context;
        _gate = gate;
    }

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        GuardedAsync(() => Context.Set<TEntity>().FindAsync([id], ct).AsTask(), ct);

    public Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<TEntity>>(async () => await Context.Set<TEntity>().ToListAsync(ct), ct);

    public Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        GuardedAsync(async () => await Context.Set<TEntity>().AddAsync(entity, ct), ct);

    public void Remove(TEntity entity) => Context.Set<TEntity>().Remove(entity);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        GuardedAsync(() => Context.SaveChangesAsync(ct), ct);

    /// <summary>
    /// Runs a single operation against <see cref="Context"/> under this app's shared
    /// <see cref="IDbAccessGate"/> — see <see cref="DbAccessGate"/>'s remarks for why every
    /// <see cref="Context"/>-touching call (base or subclass-specific query methods alike) must go
    /// through this rather than calling <see cref="Context"/> directly.
    /// </summary>
    protected async Task<T> GuardedAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        using var scope = await _gate.AcquireAsync(ct).ConfigureAwait(false);
        return await operation().ConfigureAwait(false);
    }

    /// <inheritdoc cref="GuardedAsync{T}(Func{Task{T}}, CancellationToken)"/>
    protected async Task GuardedAsync(Func<Task> operation, CancellationToken ct = default)
    {
        using var scope = await _gate.AcquireAsync(ct).ConfigureAwait(false);
        await operation().ConfigureAwait(false);
    }
}
