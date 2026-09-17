using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CardNetworks;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class CardNetworkRepository : RepositoryBase<CardNetwork>, ICardNetworkRepository
{
    public CardNetworkRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }
}
