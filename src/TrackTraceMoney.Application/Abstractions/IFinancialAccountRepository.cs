using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Application.Abstractions;

public interface IFinancialAccountRepository : IRepository<FinancialAccount>
{
    Task<IReadOnlyList<FinancialAccount>> GetActiveAsync(CancellationToken ct = default);
}
