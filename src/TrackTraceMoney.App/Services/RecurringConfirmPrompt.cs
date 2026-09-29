using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Services;

/// <summary>
/// Shared by every "confirm a recurring income" button (Recurring Incomes list, Dashboard): confirming
/// ahead of the scheduled date posts real money dated today, so ask first — a stray tap must not record
/// a paycheck that hasn't arrived. Due or overdue occurrences proceed without asking.
/// </summary>
public static class RecurringIncomeConfirmPrompt
{
    public static async Task<bool> ProceedAsync(string name, DateOnly scheduledDate, decimal amount, DateOnly today)
    {
        if (scheduledDate <= today)
            return true;

        return await Shell.Current.DisplayAlertAsync(
            AppResources.RecurringIncomes_ConfirmEarlyTitle,
            string.Format(CultureInfo.CurrentCulture, AppResources.RecurringIncomes_ConfirmEarlyMessage, name, scheduledDate, amount),
            AppResources.RecurringIncomes_ConfirmEarlyAccept,
            AppResources.RecurringIncomes_ConfirmEarlyCancel);
    }
}
