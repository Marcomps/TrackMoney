using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Builds the shared "Account → Category" (or "Account → Account" for transfers) display label for a
/// transaction. Extracted so <see cref="TransactionListItem"/> and <see cref="HistoryEntryItem"/> don't
/// each maintain their own copy of the same per-type label format.
/// </summary>
internal static class TransactionLabelFormatter
{
    public static string BuildLabel(
        Transaction transaction,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> categoryNames) =>
        transaction switch
        {
            Expense expense =>
                $"{NameOf(accountNames, expense.AccountId)} → {NameOf(categoryNames, expense.CategoryId)}",
            Income income =>
                $"{NameOf(categoryNames, income.CategoryId)} → {NameOf(accountNames, income.DestinationAccountId)}",
            Transfer transfer =>
                $"{NameOf(accountNames, transfer.SourceAccountId)} → {NameOf(accountNames, transfer.DestinationAccountId)}",
            CreditCardPurchase purchase =>
                $"{NameOf(accountNames, purchase.CreditAccountId)} → {NameOf(categoryNames, purchase.CategoryId)}",
            CreditCardPayment payment =>
                $"{NameOf(accountNames, payment.SourceAccountId)} → 💳 {NameOf(accountNames, payment.CreditAccountId)}",
            LoanPayment payment =>
                $"{NameOf(accountNames, payment.SourceAccountId)} → 🏦 {NameOf(accountNames, payment.CreditAccountId)}",
            _ => throw new NotSupportedException($"Unknown transaction type '{transaction.GetType().Name}'.")
        };

    private static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid id) =>
        names.TryGetValue(id, out var name) ? name : "?";
}
