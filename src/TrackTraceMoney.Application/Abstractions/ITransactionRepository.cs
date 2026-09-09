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

    /// <summary>
    /// Every transaction within a date range whose <see cref="Transaction.SpendAccountId"/> matches the
    /// given account — used to compute a credit card statement cycle's purchase total (README §15) without
    /// storing any transaction→statement association. Only CreditCardPurchase currently overrides
    /// SpendAccountId to a CreditAccountId (CreditCardPayment leaves it null, the base default), so this
    /// naturally excludes payments — no separate type check needed. Client-side filtered after the
    /// date-range query since SpendAccountId isn't a mapped column, same reasoning as
    /// GetByDateRangeAndCategoryAsync.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByDateRangeAndSpendAccountAsync(DateOnly from, DateOnly to, Guid spendAccountId, CancellationToken ct = default);

    /// <summary>
    /// Every CreditCardPayment in a date range targeting the given credit account (README §17
    /// "purchased vs. paid"). Unlike GetByDateRangeAndCategoryAsync/GetByDateRangeAndSpendAccountAsync,
    /// this is not a polymorphic virtual-property match — CreditAccountId is a real mapped column on
    /// CreditCardPayment specifically, so the type+column filter is pushed into the EF query itself.
    /// </summary>
    Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(
        DateOnly from, DateOnly to, Guid creditAccountId, CancellationToken ct = default);
}
