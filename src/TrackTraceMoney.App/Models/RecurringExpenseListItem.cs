using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in the full recurring-expense-definitions list (README recurring expenses story).</summary>
public sealed record RecurringExpenseListItem(
    Guid Id,
    string Name,
    decimal Amount,
    string CategoryName,
    string AccountName,
    RecurringExpenseFrequency Frequency,
    DateOnly NextDueDate,
    bool IsActive)
{
    public static RecurringExpenseListItem FromDomain(
        RecurringExpense recurringExpense,
        string categoryName,
        string accountName) =>
        new(
            recurringExpense.Id,
            recurringExpense.Name,
            recurringExpense.Amount,
            categoryName,
            accountName,
            recurringExpense.Frequency,
            recurringExpense.NextOccurrenceDate,
            recurringExpense.IsActive);
}
