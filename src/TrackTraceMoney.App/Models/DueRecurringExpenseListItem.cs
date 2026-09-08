using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in the "due now" section at the top of the recurring expenses screen.</summary>
public sealed record DueRecurringExpenseListItem(
    Guid Id,
    string Name,
    decimal Amount,
    string CategoryName,
    string AccountName,
    DateOnly OccurrenceDate)
{
    public static DueRecurringExpenseListItem FromDomain(
        RecurringExpense recurringExpense,
        string categoryName,
        string accountName) =>
        new(
            recurringExpense.Id,
            recurringExpense.Name,
            recurringExpense.Amount,
            categoryName,
            accountName,
            recurringExpense.NextOccurrenceDate);
}
