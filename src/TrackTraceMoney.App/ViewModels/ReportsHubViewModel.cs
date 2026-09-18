using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Views;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Reports hub (README §40 slice 1) — lists the 4 slice-1 report types as rows, each navigating to its
/// own report page. Reachable from Settings, not a 6th tab (see <c>SettingsViewModel.GoToManageReportsAsync</c>'s
/// remarks for why). Scales cleanly as later slices add more rows.
/// </summary>
public sealed partial class ReportsHubViewModel : ObservableObject
{
    [RelayCommand]
    private static async Task GoToExpensesByCategoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(ExpensesByCategoryReportPage));
    }

    [RelayCommand]
    private static async Task GoToIncomeVsExpensesAsync()
    {
        await Shell.Current.GoToAsync(nameof(IncomeVsExpensesReportPage));
    }

    [RelayCommand]
    private static async Task GoToBudgetVsSpendingAsync()
    {
        await Shell.Current.GoToAsync(nameof(BudgetVsSpendingReportPage));
    }

    [RelayCommand]
    private static async Task GoToMonthlyTrendAsync()
    {
        await Shell.Current.GoToAsync(nameof(MonthlyExpensesTrendReportPage));
    }
}
