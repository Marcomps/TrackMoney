using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Infrastructure.Profiles;

namespace TrackTraceMoney.Infrastructure.Profiles.Repositories;

/// <summary>
/// Sibling to <see cref="Infrastructure.Repositories.RepositoryBase{TEntity}"/> (see its remarks for
/// the full rationale), keyed to <see cref="ProfileCatalogDbContext"/>/<see cref="IProfileCatalogDbAccessGate"/>
/// instead of <see cref="Persistence.TrackTraceMoneyDbContext"/>/<see cref="Persistence.IDbAccessGate"/>.
/// Deliberately a separate type rather than a generalization of
/// <see cref="Infrastructure.Repositories.RepositoryBase{TEntity}"/> itself — that type is hardcoded to
/// the finance database and used by every existing repository; touching it risks the whole of Phase 1-3.
/// </summary>
internal abstract class ProfileCatalogRepositoryBase<TEntity> : IRepository<TEntity> where TEntity : Entity
{
    protected readonly ProfileCatalogDbContext Context;

    private readonly IProfileCatalogDbAccessGate _gate;

    protected ProfileCatalogRepositoryBase(ProfileCatalogDbContext context, IProfileCatalogDbAccessGate gate)
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
    /// Stays synchronous (matching <see cref="IRepository{TEntity}.Remove"/>'s signature) rather than
    /// going through <see cref="GuardedAsync(Func{Task},CancellationToken)"/>, for the same reason as
    /// <see cref="Infrastructure.Repositories.RepositoryBase{TEntity}.Remove"/> — see its remarks.
    /// </summary>
    public void Remove(TEntity entity)
    {
        using var scope = _gate.Acquire();
        Context.Set<TEntity>().Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        GuardedAsync(() => Context.SaveChangesAsync(ct), ct);

    /// <inheritdoc cref="Infrastructure.Repositories.RepositoryBase{TEntity}.GuardedAsync{T}(Func{Task{T}}, CancellationToken)"/>
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
