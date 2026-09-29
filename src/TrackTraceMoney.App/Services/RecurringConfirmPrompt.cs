using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Services;

/// <summary>
/// Shared by every "confirm a recurring occurrence" button (Recurring Incomes/Expenses lists, Dashboard):
/// confirming ahead of the scheduled date posts real money dated today, so ask first — a stray tap must not
/// record a paycheck that hasn't arrived or a payment that wasn't made. Due or overdue occurrences proceed
/// without asking.
/// </summary>
public static class RecurringConfirmPrompt
{
    public static Task<bool> IncomeAsync(string name, DateOnly scheduledDate, decimal amount, DateOnly today) =>
        AskIfEarlyAsync(
            scheduledDate, today,
            AppResources.RecurringIncomes_ConfirmEarlyTitle,
            string.Format(CultureInfo.CurrentCulture, AppResources.RecurringIncomes_ConfirmEarlyMessage, name, scheduledDate, amount),
            AppResources.RecurringIncomes_ConfirmEarlyAccept,
            AppResources.RecurringIncomes_ConfirmEarlyCancel);

    public static Task<bool> ExpenseAsync(string name, DateOnly scheduledDate, decimal amount, DateOnly today) =>
        AskIfEarlyAsync(
            scheduledDate, today,
            AppResources.RecurringExpenses_ConfirmEarlyTitle,
            string.Format(CultureInfo.CurrentCulture, AppResources.RecurringExpenses_ConfirmEarlyMessage, name, scheduledDate, amount),
            AppResources.RecurringExpenses_ConfirmEarlyAccept,
            AppResources.RecurringExpenses_ConfirmEarlyCancel);

    private static async Task<bool> AskIfEarlyAsync(
        DateOnly scheduledDate, DateOnly today, string title, string message, string accept, string cancel)
    {
        if (scheduledDate <= today)
            return true;

        return await Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
    }
}
