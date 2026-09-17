using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Institutions;

/// <summary>
/// A user-managed financial institution (bank, credit union/cooperativa, etc.) — an unlimited,
/// user-growable list referenced by <c>CreditCard</c> (as its issuer), <c>Loan</c>, <c>TermDeposit</c>,
/// and <c>InvestmentFund</c> (README §14/§19/§22/§23). Mirrors <see cref="Categories.Category"/>/
/// <see cref="People.Person"/>'s own shape exactly — see the financial-institution-card-network-slice-spec's
/// Decision 1 for why this is its own entity rather than a shared generic "named lookup" concept, and why
/// it is named "FinancialInstitution" rather than "Bank" (the user's own request explicitly includes
/// credit unions/cooperativas, and this folds in what was previously <c>CreditCard.Issuer</c> — a card's
/// issuer bank IS its institution, not a conceptually distinct thing).
///
/// Deliberately as simple as <see cref="Categories.Category"/>/<see cref="People.Person"/> (even simpler —
/// name only): no <c>IsActive</c>/deactivate, no delete, no uniqueness constraint on <see cref="Name"/>,
/// no search/filter. See the slice spec's "Explicitly not in scope" section for why each of those was
/// deliberately left out rather than gold-plated in.
/// </summary>
public sealed class FinancialInstitution : Entity
{
    public string Name { get; private set; } = null!;

    private FinancialInstitution()
    {
    }

    public FinancialInstitution(string name)
    {
        Rename(name);
    }

    /// <summary>
    /// Validates non-empty + max 200 chars — matches every existing <c>Institution</c>/<c>Issuer</c>
    /// free-text field's own limit (see <c>CreditCard.Issuer</c>/<c>Loan.Institution</c>/
    /// <c>TermDeposit.Institution</c>/<c>InvestmentFund.Institution</c>'s own constructor validation,
    /// which this deliberately keeps matching rather than picking a new limit now that those fields no
    /// longer own that validation themselves).
    /// </summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Financial institution name cannot be empty.", nameof(name));

        if (name.Trim().Length > 200)
            throw new ArgumentException("Financial institution name cannot exceed 200 characters.", nameof(name));

        Name = name.Trim();
    }
}
