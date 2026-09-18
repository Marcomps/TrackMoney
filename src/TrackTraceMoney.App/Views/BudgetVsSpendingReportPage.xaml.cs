using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class BudgetVsSpendingReportPage : ContentPage
{
    private readonly BudgetVsSpendingReportViewModel _viewModel;

    public BudgetVsSpendingReportPage(BudgetVsSpendingReportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
