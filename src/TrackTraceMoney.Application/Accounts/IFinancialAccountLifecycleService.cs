namespace TrackTraceMoney.Application.Accounts;

/// <summary>
/// Orchestrates the hard-delete/deactivate/reactivate/currency-edit lifecycle for a
/// <see cref="Domain.Accounts.FinancialAccount"/> (edit/delete slice spec §0/§1). Ties together the
/// Application-layer existence checks that stand in for the DB-level FK constraints this codebase
/// deliberately doesn't have (every cross-entity reference is a plain <see cref="Guid"/> property, no
/// <c>OnDelete</c>/<c>DeleteBehavior</c> configured anywhere).
/// </summary>
public interface IFinancialAccountLifecycleService
{
    /// <summary>
    /// True only if zero transactions (of any subtype) and zero recurring expense/income definitions
    /// (active or not) reference this account — see the edit/delete slice spec §1.2 for the exact
    /// three-part guard.
    /// </summary>
    Task<bool> CanHardDeleteAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes the account. Re-checks <see cref="CanHardDeleteAsync"/> itself immediately before
    /// deleting and throws <see cref="InvalidOperationException"/> if it's false — never trusts a
    /// caller's (possibly UI-cached, possibly stale) "yes".
    /// </summary>
    Task DeleteAsync(Guid accountId, CancellationToken ct = default);

    Task DeactivateAsync(Guid accountId, CancellationToken ct = default);

    Task ReactivateAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// True only if zero transactions reference this account — the same underlying check as one leg of
    /// <see cref="CanHardDeleteAsync"/>'s guard, reused here because every <c>Balance</c> figure
    /// accumulated so far is implicitly denominated in the account's current currency (edit/delete
    /// slice spec §1.1). Deliberately does NOT also check recurring expense/income references — a
    /// recurring definition alone has posted no money yet, so it carries no currency-denominated
    /// balance to silently relabel.
    /// </summary>
    Task<bool> CanChangeCurrencyAsync(Guid accountId, CancellationToken ct = default);
}
