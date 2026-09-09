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
        var accountLabel = TransactionLabelFormatter.BuildLabel(transaction, accountNames, categoryNames);

        return transaction switch
        {
            Expense expense => new TransactionListItem(
                expense.Id,
                expense.Date,
                expense.Amount,
                TransactionType.Expense,
                expense.Description,
                accountLabel),
            Income income => new TransactionListItem(
                income.Id,
                income.Date,
                income.Amount,
                TransactionType.Income,
                income.Description,
                accountLabel),
            Transfer transfer => new TransactionListItem(
                transfer.Id,
                transfer.Date,
                transfer.Amount,
                TransactionType.Transfer,
                transfer.Description,
                accountLabel),
            CreditCardPurchase purchase => new TransactionListItem(
                purchase.Id,
                purchase.Date,
                purchase.Amount,
                TransactionType.CreditCardPurchase,
                purchase.Description,
                accountLabel),
            _ => throw new NotSupportedException($"Unknown transaction type '{transaction.GetType().Name}'.")
        };
    }
}
