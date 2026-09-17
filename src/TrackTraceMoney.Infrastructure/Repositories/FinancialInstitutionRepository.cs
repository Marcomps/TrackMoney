using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Institutions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class FinancialInstitutionRepository : RepositoryBase<FinancialInstitution>, IFinancialInstitutionRepository
{
    public FinancialInstitutionRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }
}
