using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.Application.Abstractions;

public interface IRecurringExpenseRepository : IRepository<RecurringExpense>
{
    Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default);
}
