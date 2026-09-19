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

    public Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return GuardedAsync<IReadOnlyList<Transaction>>(async () => await Context.Set<Transaction>()
            .Where(t => idList.Contains(t.Id))
            .ToListAsync(ct), ct);
    }

    /// <summary>
    /// Checks every <see cref="Domain.Accounts.FinancialAccount"/>-shaped FK column across every
    /// transaction subtype, one small `Any` query per column rather than materializing any rows (see
    /// the interface doc comment for the exact column list this must stay in sync with).
    /// </summary>
    public Task<bool> HasAnyTransactionReferencingFinancialAccountAsync(Guid accountId, CancellationToken ct = default) =>
        GuardedAsync(async () =>
            await Context.Set<Transaction>().OfType<Expense>().AnyAsync(e => e.AccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<Income>().AnyAsync(i => i.DestinationAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<Transfer>().AnyAsync(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<CreditCardPayment>().AnyAsync(p => p.SourceAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<LoanPayment>().AnyAsync(p => p.SourceAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<InvestmentContribution>().AnyAsync(c => c.SourceAccountId == accountId || c.DestinationAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<InvestmentWithdrawal>().AnyAsync(w => w.SourceAccountId == accountId || w.DestinationAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<InterestIncome>().AnyAsync(i => i.DestinationAccountId == accountId, ct)
            || await Context.Set<Transaction>().OfType<Reimbursement>().AnyAsync(r => r.DestinationAccountId == accountId, ct),
            ct);

    /// <summary>
    /// Checks every <see cref="Domain.CreditAccounts.CreditAccount"/>-shaped FK column across every
    /// transaction subtype (see the interface doc comment for the exact column list).
    /// </summary>
    public Task<bool> HasAnyTransactionReferencingCreditAccountAsync(Guid creditAccountId, CancellationToken ct = default) =>
        GuardedAsync(async () =>
            await Context.Set<Transaction>().OfType<CreditCardPurchase>().AnyAsync(p => p.CreditAccountId == creditAccountId, ct)
            || await Context.Set<Transaction>().OfType<CreditCardPayment>().AnyAsync(p => p.CreditAccountId == creditAccountId, ct)
            || await Context.Set<Transaction>().OfType<LoanPayment>().AnyAsync(p => p.CreditAccountId == creditAccountId, ct),
            ct);
}
