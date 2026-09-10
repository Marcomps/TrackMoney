using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class InvestmentFundsListPage : ContentPage
{
    private readonly InvestmentFundsListViewModel _viewModel;

    public InvestmentFundsListPage(InvestmentFundsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadInvestmentFundsCommand.Execute(null);
    }
}
