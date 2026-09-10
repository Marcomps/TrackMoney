using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class InvestmentFundDetailPage : ContentPage
{
    private readonly InvestmentFundDetailViewModel _viewModel;

    public InvestmentFundDetailPage(InvestmentFundDetailViewModel viewModel)
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
