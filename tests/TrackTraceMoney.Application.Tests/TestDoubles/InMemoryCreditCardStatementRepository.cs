using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.Tests.TestDoubles;

/// <summary>Shared in-memory statement store for services that consult the latest recorded statement.</summary>
internal sealed class InMemoryCreditCardStatementRepository : ICreditCardStatementRepository
{
    private readonly List<CreditCardStatement> _statements = [];

    public CreditCardStatement Add(CreditCardStatement statement)
    {
        _statements.Add(statement);
        return statement;
    }

    public Task<CreditCardStatement?> GetLatestForCardAsync(Guid creditAccountId, CancellationToken ct = default) =>
        Task.FromResult(_statements.Where(s => s.CreditAccountId == creditAccountId).MaxBy(s => s.CycleEndDate));

    public Task<IReadOnlyList<CreditCardStatement>> GetForCardAsync(Guid creditAccountId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CreditCardStatement>>(_statements.Where(s => s.CreditAccountId == creditAccountId).ToList());

    public Task<IReadOnlyDictionary<Guid, CreditCardStatement>> GetLatestForCardsAsync(IEnumerable<Guid> creditAccountIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CreditCardStatement>>(creditAccountIds
            .Select(id => _statements.Where(s => s.CreditAccountId == id).MaxBy(s => s.CycleEndDate))
            .OfType<CreditCardStatement>()
            .ToDictionary(s => s.CreditAccountId));

    public Task<CreditCardStatement?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_statements.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<CreditCardStatement>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CreditCardStatement>>(_statements.ToList());

    public Task AddAsync(CreditCardStatement entity, CancellationToken ct = default)
    {
        _statements.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(CreditCardStatement entity) => _statements.Remove(entity);

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
