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

    /// <summary>
    /// Batched counterpart to <see cref="IRepository{TEntity}.GetByIdAsync"/> — every <see cref="Transaction"/>
    /// whose id is in <paramref name="ids"/>, in one query, instead of one <c>GetByIdAsync</c> call per id
    /// in a loop (the same anti-N+1 pattern as <see cref="GetCreditCardPaymentsUpToDateForCreditAccountsAsync"/>).
    /// Used to resolve the handful of transactions behind a filtered set of pending
    /// <see cref="Domain.MedicalExpenses.MedicalExpenseDetail"/> rows without a full-table
    /// <see cref="IRepository{TEntity}.GetAllAsync"/> fetch.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Whether any transaction, of any subtype, still points at <paramref name="accountId"/> as a
    /// <see cref="Domain.Accounts.FinancialAccount"/>-shaped foreign key (edit/delete slice spec §0) —
    /// checked before allowing a hard delete or a currency edit on that account. Every FK-shaped column
    /// across every subtype is covered, not just the in-scope-for-reversal <see cref="Expense"/>/
    /// <see cref="Income"/>/<see cref="Transfer"/> types: <see cref="Expense.AccountId"/>,
    /// <see cref="Income.DestinationAccountId"/>, <see cref="Transfer.SourceAccountId"/>/
    /// <see cref="Transfer.DestinationAccountId"/>, <see cref="CreditCardPayment.SourceAccountId"/>,
    /// <see cref="LoanPayment.SourceAccountId"/>, <see cref="InvestmentContribution.SourceAccountId"/>/
    /// <see cref="InvestmentContribution.DestinationAccountId"/>,
    /// <see cref="InvestmentWithdrawal.SourceAccountId"/>/<see cref="InvestmentWithdrawal.DestinationAccountId"/>,
    /// <see cref="InterestIncome.DestinationAccountId"/>, and <see cref="Reimbursement.DestinationAccountId"/>
    /// — deleting an account referenced by any of them, even a type this slice can't edit/reverse, would
    /// orphan that transaction.
    /// </summary>
    Task<bool> HasAnyTransactionReferencingFinancialAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Whether any transaction still points at <paramref name="creditAccountId"/> as a
    /// <see cref="Domain.CreditAccounts.CreditAccount"/>-shaped foreign key (edit/delete slice spec §0):
    /// <see cref="CreditCardPurchase.CreditAccountId"/>, <see cref="CreditCardPayment.CreditAccountId"/>,
    /// or <see cref="LoanPayment.CreditAccountId"/> — checked before allowing a hard delete or a
    /// currency edit on that card/loan.
    /// </summary>
    Task<bool> HasAnyTransactionReferencingCreditAccountAsync(Guid creditAccountId, CancellationToken ct = default);

    /// <summary>
    /// Whether any transaction still points at <paramref name="categoryId"/> (Category lifecycle slice
    /// §A.3) -- checked before allowing a hard delete or deactivate of that category. Only
    /// <see cref="Expense"/>, <see cref="Income"/>, and <see cref="CreditCardPurchase"/> currently carry
    /// a <c>CategoryId</c>-shaped foreign key (confirmed by grepping every <see cref="Transaction"/>
    /// subtype); every other subtype (Transfer, CreditCardPayment, LoanPayment, Investment*,
    /// InterestIncome, Reimbursement) has no category concept at all.
    /// </summary>
    Task<bool> HasAnyTransactionReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default);
}
