using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.People;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class PersonRepository : RepositoryBase<Person>, IPersonRepository
{
    public PersonRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }
}
