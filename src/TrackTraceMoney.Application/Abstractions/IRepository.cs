using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Application.Abstractions;

public interface IRepository<TEntity> where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(TEntity entity, CancellationToken ct = default);

    void Remove(TEntity entity);

    Task SaveChangesAsync(CancellationToken ct = default);
}
