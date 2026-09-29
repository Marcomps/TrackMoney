using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Services;

/// <summary>
/// Shared by every "confirm a recurring occurrence" button (Recurring Incomes/Expenses lists, Dashboard).
/// A due or overdue occurrence is confirmed as-is (dated on its schedule). One confirmed ahead of its date
/// asks WHEN it actually happened — today, yesterday, ... back to the earliest allowed day — so the
/// transaction carries the real date instead of having to be edited afterwards. Cancelling returns null.
/// </summary>
public static class RecurringConfirmPrompt
{
    public static Task<DateOnly?> IncomeDateAsync(string name, decimal amount, DateOnly scheduledDate, DateOnly earliestDate, DateOnly today) =>
        AskDateIfEarlyAsync(
            string.Format(CultureInfo.CurrentCulture, AppResources.RecurringIncomes_WhenReceivedTitleFormat, name, amount),
            scheduledDate, earliestDate, today);

    public static Task<DateOnly?> ExpenseDateAsync(string name, decimal amount, DateOnly scheduledDate, DateOnly earliestDate, DateOnly today) =>
        AskDateIfEarlyAsync(
            string.Format(CultureInfo.CurrentCulture, AppResources.RecurringExpenses_WhenPaidTitleFormat, name, amount),
            scheduledDate, earliestDate, today);

    private static async Task<DateOnly?> AskDateIfEarlyAsync(string title, DateOnly scheduledDate, DateOnly earliestDate, DateOnly today)
    {
        if (scheduledDate <= today)
            return today;

        var options = new List<(string Label, DateOnly Date)>();
        for (var day = today; day >= earliestDate; day = day.AddDays(-1))
            options.Add((DayLabel(day, today), day));

        var choice = await Shell.Current.DisplayActionSheetAsync(
            title, AppResources.RecurringConfirm_Cancel, null, options.Select(o => o.Label).ToArray());

        var picked = options.FirstOrDefault(o => o.Label == choice);
        return picked.Label is null ? null : picked.Date;
    }

    private static string DayLabel(DateOnly day, DateOnly today)
    {
        var format = day == today ? AppResources.RecurringConfirm_TodayFormat
            : day == today.AddDays(-1) ? AppResources.RecurringConfirm_YesterdayFormat
            : AppResources.RecurringConfirm_DayFormat;

        var label = string.Format(CultureInfo.CurrentCulture, format, day);
        return char.ToUpper(label[0], CultureInfo.CurrentCulture) + label[1..];
    }
}
