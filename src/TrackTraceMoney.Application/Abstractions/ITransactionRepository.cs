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
    /// not just <c>Expense</c>. <c>SpendCategoryId</c> itself is a virtual C# property rather than a
    /// mapped column and can't be pushed into SQL directly, so the implementation queries each concrete
    /// type that currently overrides it (<c>Expense</c>, <c>CreditCardPurchase</c>) separately against
    /// that type's own mapped <c>CategoryId</c> column and concatenates the results — not a full
    /// date-range fetch filtered client-side. Used for the budget-crossing check (README §34/§37), which
    /// only ever needs one category's spend, not every transaction across every account/category for the
    /// period.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Every transaction within a date range whose <see cref="Transaction.SpendAccountId"/> matches the
    /// given account — used to compute a credit card statement cycle's purchase total (README §15) without
    /// storing any transaction→statement association. Only CreditCardPurchase currently overrides
    /// SpendAccountId to a CreditAccountId (CreditCardPayment leaves it null, the base default), so this
    /// naturally excludes payments — no separate type check needed. Same querying strategy as
    /// GetByDateRangeAndCategoryAsync: queries each overriding concrete type against its own mapped
    /// column (Expense.AccountId, CreditCardPurchase.CreditAccountId) rather than fetching the date range
    /// and filtering client-side on the virtual property.
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

    /// <summary>
    /// Batched counterpart to <see cref="GetCreditCardPaymentsByDateRangeAndCreditAccountAsync"/> — every
    /// <see cref="CreditCardPayment"/> up to (and including) <paramref name="to"/> targeting any of
    /// <paramref name="creditAccountIds"/>, in one query, instead of one query per card (avoids the N+1
    /// pattern that used to sit in <c>CreditCardsListViewModel</c>'s per-card loop). Deliberately has no
    /// lower date bound: each card's own cycle-start lower bound differs (it's that card's latest
    /// statement's <c>CycleEndDate</c>), so callers that need per-card date ranges filter the returned
    /// (already small, upper-bounded and id-filtered) result client-side per card rather than this method
    /// trying to accept one date range per id.
    /// </summary>
    Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsUpToDateForCreditAccountsAsync(
        DateOnly to, IEnumerable<Guid> creditAccountIds, CancellationToken ct = default);
}
