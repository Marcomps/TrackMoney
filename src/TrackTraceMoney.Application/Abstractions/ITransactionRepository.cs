using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Abstractions;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Returns only the <see cref="Domain.Transactions.Expense"/> transactions for one category within
    /// a date range, filtered at the query level rather than fetched wholesale and filtered client-side.
    /// Used for the budget-crossing check (README §34/§37), which only ever needs one category's spend,
    /// not every transaction across every account/category for the period.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default);
}
