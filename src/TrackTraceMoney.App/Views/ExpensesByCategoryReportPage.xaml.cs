using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class ExpensesByCategoryReportPage : ContentPage
{
    private readonly ExpensesByCategoryReportViewModel _viewModel;

    public ExpensesByCategoryReportPage(ExpensesByCategoryReportViewModel viewModel)
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
