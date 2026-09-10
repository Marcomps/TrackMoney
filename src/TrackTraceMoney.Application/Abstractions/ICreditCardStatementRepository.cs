using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.Abstractions;

public interface ICreditCardStatementRepository : IRepository<CreditCardStatement>
{
    Task<CreditCardStatement?> GetLatestForCardAsync(Guid creditAccountId, CancellationToken ct = default);

    Task<IReadOnlyList<CreditCardStatement>> GetForCardAsync(Guid creditAccountId, CancellationToken ct = default);

    /// <summary>
    /// Batched counterpart to <see cref="GetLatestForCardAsync"/> — one query for the latest statement
    /// (highest <see cref="CreditCardStatement.CycleEndDate"/>) per card in <paramref name="creditAccountIds"/>,
    /// instead of one query per card (avoids the N+1 pattern that used to sit in
    /// <c>CreditCardsListViewModel</c>/<c>SnowballPlanViewModel</c>'s per-card loops). A card with no
    /// statement at all is simply absent from the result, mirroring <see cref="GetLatestForCardAsync"/>
    /// returning null for that card.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, CreditCardStatement>> GetLatestForCardsAsync(IEnumerable<Guid> creditAccountIds, CancellationToken ct = default);
}
