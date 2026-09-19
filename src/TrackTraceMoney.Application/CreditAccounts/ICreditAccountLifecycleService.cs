namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// Orchestrates the hard-delete/deactivate/reactivate/currency-edit lifecycle for a
/// <see cref="Domain.CreditAccounts.CreditAccount"/> (edit/delete slice spec §0/§2/§3) — covers both
/// <see cref="Domain.CreditAccounts.CreditCard"/> and <see cref="Domain.CreditAccounts.Loan"/>
/// polymorphically, the same way <see cref="Abstractions.ICreditAccountRepository"/> already does.
/// </summary>
public interface ICreditAccountLifecycleService
{
    /// <summary>
    /// True only if zero transactions reference this credit account, zero recurring expense
    /// definitions (active or not) reference it, and — for a <see cref="Domain.CreditAccounts.CreditCard"/>
    /// specifically — it has zero <see cref="Domain.CreditAccounts.CreditCardStatement"/> rows (a
    /// <see cref="Domain.CreditAccounts.Loan"/> has no statement concept, so that third check is
    /// skipped for loans). See the edit/delete slice spec §2.2/§3.2.
    /// </summary>
    Task<bool> CanHardDeleteAsync(Guid creditAccountId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes the credit account. Re-checks <see cref="CanHardDeleteAsync"/> itself immediately
    /// before deleting and throws <see cref="InvalidOperationException"/> if it's false — never trusts
    /// a caller's cached "yes".
    /// </summary>
    Task DeleteAsync(Guid creditAccountId, CancellationToken ct = default);

    Task DeactivateAsync(Guid creditAccountId, CancellationToken ct = default);

    Task ReactivateAsync(Guid creditAccountId, CancellationToken ct = default);

    /// <summary>
    /// True only if zero transactions reference this credit account — the same underlying check as one
    /// leg of <see cref="CanHardDeleteAsync"/>'s guard, reused for the same reason as
    /// <see cref="Accounts.IFinancialAccountLifecycleService.CanChangeCurrencyAsync"/>.
    /// </summary>
    Task<bool> CanChangeCurrencyAsync(Guid creditAccountId, CancellationToken ct = default);
}
