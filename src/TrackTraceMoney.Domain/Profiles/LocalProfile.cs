using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Profiles;

/// <summary>
/// One local, password-less profile — its own independent on-device dataset, backed by its own SQLite
/// file (<c>tracktracemoney_{Id}.db3</c>, opened through the existing, unchanged
/// <c>TrackTraceMoneyDbContext</c>). This entity itself lives only in the small, always-open
/// "catalog" database (<c>tracktracemoney_profiles.db3</c>) that lists which profiles exist — it has
/// no relationship to and is never persisted alongside the finance schema it describes.
///
/// Deliberately has no password/email field — this feature is intentionally password-less. Not to be
/// confused with <c>Person</c> (README §7/§26), which tracks who paid/who an expense was for *within*
/// one profile's shared dataset.
/// </summary>
public sealed class LocalProfile : Entity
{
    public Guid LocalProfileGroupId { get; private set; }

    public string Name { get; private set; } = null!;

    private LocalProfile()
    {
        // EF
    }

    public LocalProfile(Guid localProfileGroupId, string name)
    {
        if (localProfileGroupId == Guid.Empty)
            throw new ArgumentException("A profile must belong to a group.", nameof(localProfileGroupId));

        LocalProfileGroupId = localProfileGroupId;
        Rename(name);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));

        Name = name.Trim();
    }
}
