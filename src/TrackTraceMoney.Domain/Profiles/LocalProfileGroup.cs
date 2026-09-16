using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Profiles;

/// <summary>
/// A named grouping of <see cref="LocalProfile"/>s within one device's local, password-less
/// multi-profile catalog — completely unrelated to the cloud "Cuenta en la nube" login. Slice 1 of
/// this feature only ever has exactly one group per device (created silently by the app, never shown
/// in UI), but it is modeled as a real row with its own identity — rather than flattened away — so a
/// later slice can expose multiple groups without any schema change.
/// </summary>
public sealed class LocalProfileGroup : Entity
{
    public string Name { get; private set; } = null!;

    private LocalProfileGroup()
    {
        // EF
    }

    public LocalProfileGroup(string name) => Rename(name);

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.", nameof(name));

        Name = name.Trim();
    }
}
