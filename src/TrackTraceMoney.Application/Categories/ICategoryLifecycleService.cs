namespace TrackTraceMoney.Application.Categories;

/// <summary>
/// Orchestrates the hard-delete/deactivate/reactivate lifecycle for a
/// <see cref="Domain.Categories.Category"/> (Category lifecycle slice §A.3), mirroring
/// <see cref="Accounts.IFinancialAccountLifecycleService"/>'s file-for-file shape. Ties together the
/// Application-layer existence checks that stand in for the DB-level FK constraints this codebase
/// deliberately doesn't have.
/// </summary>
public interface ICategoryLifecycleService
{
    /// <summary>
    /// True only if zero Expense/Income/CreditCardPurchase transactions, zero RecurringExpense/
    /// RecurringIncome definitions (active or not), and zero Budget rows reference this category --
    /// the six reference sites confirmed by grepping <c>CategoryId</c> across <c>Domain/</c>.
    /// </summary>
    Task<bool> CanHardDeleteAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes the category. Re-checks <see cref="CanHardDeleteAsync"/> itself immediately before
    /// deleting and throws <see cref="InvalidOperationException"/> if it's false -- never trusts a
    /// caller's (possibly UI-cached, possibly stale) "yes". Also throws for a system-defined category,
    /// defensively, even though the UI should never offer this action for one.
    /// </summary>
    Task DeleteAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> for a system-defined category, defensively, even
    /// though the UI should never offer this action for one -- system categories are the baseline
    /// vocabulary every budget/report screen expects to exist.
    /// </summary>
    Task DeactivateAsync(Guid categoryId, CancellationToken ct = default);

    Task ReactivateAsync(Guid categoryId, CancellationToken ct = default);
}
