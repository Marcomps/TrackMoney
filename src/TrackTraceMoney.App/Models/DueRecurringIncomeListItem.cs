using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// A row in the "to confirm" section at the top of the recurring incomes screen: either already due,
/// or <see cref="IsEarly"/> — scheduled within the early-confirmation window (payroll paid ahead of a
/// weekend payday).
/// </summary>
public sealed record DueRecurringIncomeListItem(
    Guid Id,
    string Name,
    decimal Amount,
    string CategoryName,
    string AccountName,
    DateOnly OccurrenceDate,
    bool IsEarly)
{
    /// <summary>
    /// Builds a "due now" row from an already-built <see cref="RecurringIncomeListItem"/>, reusing
    /// its already-resolved category/account display names instead of resolving them a second time.
    /// </summary>
    public static DueRecurringIncomeListItem FromListItem(RecurringIncomeListItem listItem, DateOnly today) =>
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
