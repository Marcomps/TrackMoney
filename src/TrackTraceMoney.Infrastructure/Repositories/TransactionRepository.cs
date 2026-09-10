using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class TransactionRepository : RepositoryBase<Transaction>, ITransactionRepository
{
    public TransactionRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Transaction>>(async () => await Context.Set<Transaction>()
            .Where(t => t.Date >= from && t.Date <= to)
            .ToListAsync(ct), ct);

    /// <summary>
    /// Only <see cref="Expense"/> and <see cref="CreditCardPurchase"/> currently override
    /// <see cref="Transaction.SpendCategoryId"/> to a non-null value (verified by reading every
    /// <see cref="Transaction"/> subtype) — queried separately via <c>OfType&lt;T&gt;()</c> so each hits
    /// its own mapped <c>CategoryId</c> column in real SQL, then concatenated in memory, instead of
    /// materializing the whole date range and filtering client-side on the virtual property (which
    /// can't translate to SQL) like this method used to.
    /// </summary>
    public Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Transaction>>(async () =>
        {
            var expenses = await Context.Set<Transaction>()
                .Where(t => t.Date >= from && t.Date <= to)
                .OfType<Expense>()
                .Where(e => e.CategoryId == categoryId)
                .ToListAsync(ct);
            var purchases = await Context.Set<Transaction>()
                .Where(t => t.Date >= from && t.Date <= to)
                .OfType<CreditCardPurchase>()
                .Where(p => p.CategoryId == categoryId)
                .ToListAsync(ct);
            return expenses.Cast<Transaction>().Concat(purchases).ToList();
        }, ct);

    /// <summary>
    /// Same rationale as <see cref="GetByDateRangeAndCategoryAsync"/>, for <see cref="Transaction.SpendAccountId"/>:
    /// only <see cref="Expense"/> (via <see cref="Expense.AccountId"/>) and <see cref="CreditCardPurchase"/>
    /// (via <see cref="CreditCardPurchase.CreditAccountId"/>) override it, so each is queried against its
    /// own mapped column and the results concatenated in memory rather than fetch-then-filter.
    /// </summary>
    public Task<IReadOnlyList<Transaction>> GetByDateRangeAndSpendAccountAsync(DateOnly from, DateOnly to, Guid spendAccountId, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<Transaction>>(async () =>
        {
            var expenses = await Context.Set<Transaction>()
                .Where(t => t.Date >= from && t.Date <= to)
                .OfType<Expense>()
                .Where(e => e.AccountId == spendAccountId)
                .ToListAsync(ct);
            var purchases = await Context.Set<Transaction>()
                .Where(t => t.Date >= from && t.Date <= to)
                .OfType<CreditCardPurchase>()
                .Where(p => p.CreditAccountId == spendAccountId)
                .ToListAsync(ct);
            return expenses.Cast<Transaction>().Concat(purchases).ToList();
        }, ct);

    public Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(
        DateOnly from, DateOnly to, Guid creditAccountId, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<CreditCardPayment>>(async () => await Context.Set<Transaction>()
            .Where(t => t.Date >= from && t.Date <= to)
            .OfType<CreditCardPayment>()
            .Where(p => p.CreditAccountId == creditAccountId)
            .ToListAsync(ct), ct);

    public Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsUpToDateForCreditAccountsAsync(
        DateOnly to, IEnumerable<Guid> creditAccountIds, CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<CreditCardPayment>>(async () =>
        {
            var ids = creditAccountIds.ToList();
            return await Context.Set<Transaction>()
                .Where(t => t.Date <= to)
                .OfType<CreditCardPayment>()
                .Where(p => ids.Contains(p.CreditAccountId))
                .ToListAsync(ct);
        }, ct);
}
