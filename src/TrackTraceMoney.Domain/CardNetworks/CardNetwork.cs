using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.CardNetworks;

/// <summary>
/// A user-managed card network/brand (e.g. Visa, Mastercard) — an unlimited, user-growable list
/// referenced only by <c>CreditCard.NetworkId</c> (README §14). Slice B of the
/// financial-institution-card-network-slice-spec: small, clean, additive, CreditCard-only, structurally
/// disjoint from <see cref="Institutions.FinancialInstitution"/> (Slice A) except for sharing the
/// <c>AddCreditCardPage</c>/<c>SettingsPage</c> UI surface (App-layer work, not part of this layer).
///
/// Same deliberately-minimal shape as <see cref="Institutions.FinancialInstitution"/> — name only, no
/// <c>IsActive</c>/deactivate, no delete, no uniqueness constraint, no search/filter, no seed data (not
/// even Visa/Mastercard — see the slice spec's Decision 1 for why seeding even two well-known networks
/// was rejected).
/// </summary>
public sealed class CardNetwork : Entity
{
    public string Name { get; private set; } = null!;

    private CardNetwork()
    {
    }

    public CardNetwork(string name)
    {
        Rename(name);
    }

    /// <summary>Validates non-empty + max 200 chars — see <see cref="Institutions.FinancialInstitution.Rename"/>'s
    /// remarks for why this matches every existing free-text institution/issuer field's own limit.</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card network name cannot be empty.", nameof(name));

        if (name.Trim().Length > 200)
            throw new ArgumentException("Card network name cannot exceed 200 characters.", nameof(name));

        Name = name.Trim();
    }
}
