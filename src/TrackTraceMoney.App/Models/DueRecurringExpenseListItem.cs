using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// A row in the "to confirm" section at the top of the recurring expenses screen: either already due,
/// or <see cref="IsEarly"/> — scheduled within the early-confirmation window (paid a few days ahead).
/// </summary>
public sealed record DueRecurringExpenseListItem(
    Guid Id,
    string Name,
    decimal Amount,
    string CategoryName,
    string AccountName,
    DateOnly OccurrenceDate,
    bool IsEarly)
{
    public static DueRecurringExpenseListItem FromListItem(RecurringExpenseListItem listItem, DateOnly today) =>
        new(
            listItem.Id,
            listItem.Name,
            listItem.Amount,
            listItem.CategoryName,
            listItem.AccountName,
            listItem.NextDueDate,
            listItem.NextDueDate > today);

    /// <summary>Shown under early rows only, so it's clear the payment isn't due yet.</summary>
    public string ScheduledForText =>
        string.Format(CultureInfo.CurrentCulture, AppResources.RecurringIncomes_ScheduledForFormat, OccurrenceDate);
}
