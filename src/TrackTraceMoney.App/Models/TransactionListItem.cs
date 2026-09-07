using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.App.Models;

public sealed record TransactionListItem(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string? Description,
    string AccountLabel)
{
    public static TransactionListItem FromDomain(
        Transaction transaction,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid id) =>
            names.TryGetValue(id, out var name) ? name : "?";

        return transaction switch
        {
            Expense expense => new TransactionListItem(
                expense.Id,
                expense.Date,
                expense.Amount,
                TransactionType.Expense,
                expense.Description,
                $"{NameOf(accountNames, expense.AccountId)} → {NameOf(categoryNames, expense.CategoryId)}"),
            Income income => new TransactionListItem(
                income.Id,
                income.Date,
                income.Amount,
                TransactionType.Income,
                income.Description,
                $"{NameOf(categoryNames, income.CategoryId)} → {NameOf(accountNames, income.DestinationAccountId)}"),
            Transfer transfer => new TransactionListItem(
                transfer.Id,
                transfer.Date,
                transfer.Amount,
                TransactionType.Transfer,
                transfer.Description,
                $"{NameOf(accountNames, transfer.SourceAccountId)} → {NameOf(accountNames, transfer.DestinationAccountId)}"),
            _ => throw new NotSupportedException($"Unknown transaction type '{transaction.GetType().Name}'.")
        };
    }
}
