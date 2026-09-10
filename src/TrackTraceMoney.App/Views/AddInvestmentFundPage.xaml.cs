using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddInvestmentFundPage : ContentPage
{
    public AddInvestmentFundPage(AddInvestmentFundViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
