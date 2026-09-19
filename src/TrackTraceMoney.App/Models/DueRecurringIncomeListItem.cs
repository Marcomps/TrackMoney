namespace TrackTraceMoney.App.Models;

/// <summary>A row in the "due now" section at the top of the recurring incomes screen.</summary>
public sealed record DueRecurringIncomeListItem(
    Guid Id,
    string Name,
    decimal Amount,
    string CategoryName,
    string AccountName,
    DateOnly OccurrenceDate)
{
    /// <summary>
    /// Builds a "due now" row from an already-built <see cref="RecurringIncomeListItem"/>, reusing
    /// its already-resolved category/account display names instead of resolving them a second time.
    /// </summary>
    public static DueRecurringIncomeListItem FromListItem(RecurringIncomeListItem listItem) =>
        new(
            listItem.Id,
            listItem.Name,
            listItem.Amount,
            listItem.CategoryName,
            listItem.AccountName,
            listItem.NextDueDate);
}
