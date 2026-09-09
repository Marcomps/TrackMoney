using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.Abstractions;

public interface ICreditCardStatementRepository : IRepository<CreditCardStatement>
{
    Task<CreditCardStatement?> GetLatestForCardAsync(Guid creditAccountId, CancellationToken ct = default);

    Task<IReadOnlyList<CreditCardStatement>> GetForCardAsync(Guid creditAccountId, CancellationToken ct = default);
}
