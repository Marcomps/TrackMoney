using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Profiles;
using TrackTraceMoney.Infrastructure.Profiles;

namespace TrackTraceMoney.Infrastructure.Profiles.Repositories;

internal sealed class LocalProfileRepository : ProfileCatalogRepositoryBase<LocalProfile>, ILocalProfileRepository
{
    public LocalProfileRepository(ProfileCatalogDbContext context, IProfileCatalogDbAccessGate gate) : base(context, gate)
    {
    }
}
