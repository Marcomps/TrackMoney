using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.Application.Abstractions;

public interface IRecurringIncomeRepository : IRepository<RecurringIncome>
{
    Task<IReadOnlyList<RecurringIncome>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether any <see cref="RecurringIncome"/> row — active or not — references
    /// <paramref name="accountId"/> via <see cref="RecurringIncome.DestinationAccountId"/> (edit/delete
    /// slice spec §0). Deliberately includes inactive rows, mirroring
    /// <see cref="IRecurringExpenseRepository.HasAnyReferencingAccountAsync"/>'s same rationale.
    /// </summary>
    Task<bool> HasAnyReferencingAccountAsync(Guid accountId, CancellationToken ct = default);
}
