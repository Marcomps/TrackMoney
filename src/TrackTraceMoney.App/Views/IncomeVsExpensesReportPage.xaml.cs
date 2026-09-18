using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class IncomeVsExpensesReportPage : ContentPage
{
    private readonly IncomeVsExpensesReportViewModel _viewModel;

    public IncomeVsExpensesReportPage(IncomeVsExpensesReportViewModel viewModel)
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
