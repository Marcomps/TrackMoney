using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// A single row in the History screen (README §39). Carries the same display fields as
/// <see cref="TransactionListItem"/> plus the raw filter keys (accounts/category/people involved)
/// needed for client-side filtering — this app filters in-memory over the full local transaction
/// set rather than pushing filters down to SQLite, since offline-first MVP volume doesn't need it.
/// </summary>
public sealed record HistoryEntryItem(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string? Description,
    string AccountLabel,
    IReadOnlyList<Guid> AccountIds,
    Guid? CategoryId,
    IReadOnlyList<Guid> PersonIds)
{
    public static HistoryEntryItem FromDomain(
        Transaction transaction,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        var accountLabel = TransactionLabelFormatter.BuildLabel(transaction, accountNames, categoryNames);

        return transaction switch
        {
            Expense expense => new HistoryEntryItem(
                expense.Id,
                expense.Date,
                expense.Amount,
                TransactionType.Expense,
                expense.Description,
                accountLabel,
                [expense.AccountId],
                expense.CategoryId,
                BuildPersonIds(expense.PayerPersonId, expense.BeneficiaryPersonId)),
            Income income => new HistoryEntryItem(
                income.Id,
                income.Date,
                income.Amount,
                TransactionType.Income,
                income.Description,
                accountLabel,
                [income.DestinationAccountId],
                income.CategoryId,
                BuildPersonIds(income.PersonId)),
            Transfer transfer => new HistoryEntryItem(
                transfer.Id,
                transfer.Date,
                transfer.Amount,
                TransactionType.Transfer,
                transfer.Description,
                accountLabel,
                [transfer.SourceAccountId, transfer.DestinationAccountId],
                null,
                []),
            CreditCardPurchase purchase => new HistoryEntryItem(
                purchase.Id,
                purchase.Date,
                purchase.Amount,
                TransactionType.CreditCardPurchase,
                purchase.Description,
                accountLabel,
                [purchase.CreditAccountId],
                purchase.CategoryId,
                BuildPersonIds(purchase.PayerPersonId, purchase.BeneficiaryPersonId)),
            _ => throw new NotSupportedException($"Unknown transaction type '{transaction.GetType().Name}'.")
        };
    }

    private static IReadOnlyList<Guid> BuildPersonIds(params Guid?[] personIds) =>
        personIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
}
