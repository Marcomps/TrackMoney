using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddInvestmentFundPage : ContentPage
{
    private readonly AddInvestmentFundViewModel _viewModel;

    public AddInvestmentFundPage(AddInvestmentFundViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadOptionsCommand.Execute(null);
    }
}
