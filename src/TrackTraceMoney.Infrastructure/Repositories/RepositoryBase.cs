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

    /// <summary>
    /// Unlike the other members here, this stays synchronous (matching
    /// <see cref="IRepository{TEntity}.Remove"/>'s signature) rather than going through
    /// <see cref="GuardedAsync(Func{Task},CancellationToken)"/>, because it never awaits — it only
    /// mutates <see cref="Context"/>'s in-memory change tracker, it does not touch the database
    /// connection. It still must not run concurrently with any other <see cref="Context"/>-touching
    /// call, so it acquires <see cref="IDbAccessGate.Acquire"/> (the synchronous counterpart to the
    /// async gate every other method here uses) around that mutation — see <see cref="DbAccessGate"/>'s
    /// remarks.
    /// </summary>
    public void Remove(TEntity entity)
    {
        using var scope = _gate.Acquire();
        Context.Set<TEntity>().Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        GuardedAsync(() => Context.SaveChangesAsync(ct), ct);

    /// <summary>
    /// Runs a single async operation against <see cref="Context"/> under this app's shared
    /// <see cref="IDbAccessGate"/> — see <see cref="DbAccessGate"/>'s remarks for why every
    /// <see cref="Context"/>-touching call (base or subclass-specific query methods alike) must go
    /// through the gate rather than calling <see cref="Context"/> directly. Every such call in this
    /// class does — the only exception is the synchronous <see cref="Remove"/>, which acquires the
    /// same gate via <see cref="IDbAccessGate.Acquire"/> instead of this helper, since it has no
    /// <see cref="Task"/> to hand back.
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
