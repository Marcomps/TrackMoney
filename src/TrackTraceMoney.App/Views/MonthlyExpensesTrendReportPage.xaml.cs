using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class MonthlyExpensesTrendReportPage : ContentPage
{
    private readonly MonthlyExpensesTrendReportViewModel _viewModel;

    public MonthlyExpensesTrendReportPage(MonthlyExpensesTrendReportViewModel viewModel)
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
