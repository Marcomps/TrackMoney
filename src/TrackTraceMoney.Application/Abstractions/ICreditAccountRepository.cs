using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.Abstractions;

public interface ICreditAccountRepository : IRepository<CreditAccount>
{
    Task<IReadOnlyList<CreditAccount>> GetActiveAsync(CancellationToken ct = default);
}
