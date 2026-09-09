using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Abstractions;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Returns every transaction within a date range whose <see cref="Domain.Transactions.Transaction.SpendCategoryId"/>
    /// matches the given category — type-agnostic, so it picks up every spend-counting transaction kind
    /// (<see cref="Domain.Transactions.Expense"/>, <see cref="Domain.Transactions.CreditCardPurchase"/>,
    /// and any future kind that opts into <see cref="Domain.Transactions.Transaction.CountsAsExpense"/>),
    /// not just <c>Expense</c>. <c>SpendCategoryId</c> is a virtual C# property rather than a mapped
    /// column, so the category filter is applied client-side after fetching the date range — acceptable
    /// at local-SQLite MVP volume (mirrors in-memory filtering already used elsewhere, e.g.
    /// <c>HistoryViewModel</c>). Used for the budget-crossing check (README §34/§37), which only ever
    /// needs one category's spend, not every transaction across every account/category for the period.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default);
}
