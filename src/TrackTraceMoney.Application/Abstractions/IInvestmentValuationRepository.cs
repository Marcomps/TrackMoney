using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Application.Abstractions;

public interface IInvestmentValuationRepository : IRepository<InvestmentValuation>
{
    Task<IReadOnlyList<InvestmentValuation>> GetForFundAsync(Guid investmentFundId, CancellationToken ct = default);
}
