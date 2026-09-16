using TrackTraceMoney.Domain.Profiles;

namespace TrackTraceMoney.App.Models;

public sealed record ProfileListItem(Guid Id, string Name, bool IsActive)
{
    public static ProfileListItem FromDomain(LocalProfile profile, bool isActive) =>
        new(profile.Id, profile.Name, isActive);
}
