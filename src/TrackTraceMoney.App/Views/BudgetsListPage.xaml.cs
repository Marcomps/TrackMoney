using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class BudgetsListPage : ContentPage
{
    private readonly BudgetsListViewModel _viewModel;

    public BudgetsListPage(BudgetsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadBudgetsCommand.Execute(null);
    }
}
