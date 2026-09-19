using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.Application.Abstractions;

public interface IRecurringExpenseRepository : IRepository<RecurringExpense>
{
    Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether any <see cref="RecurringExpense"/> row — active or not — references
    /// <paramref name="accountOrCreditAccountId"/> via either <see cref="RecurringExpense.AccountId"/>
    /// or <see cref="RecurringExpense.CreditAccountId"/> (edit/delete slice spec §0; exactly one of the
    /// two is ever set per row, per the entity's own XOR invariant, so checking both covers every row
    /// regardless of which hierarchy it targets). Deliberately includes inactive rows: a deactivated
    /// recurring expense's account reference would still dangle if the account were hard-deleted.
    /// </summary>
    Task<bool> HasAnyReferencingAccountAsync(Guid accountOrCreditAccountId, CancellationToken ct = default);
}
