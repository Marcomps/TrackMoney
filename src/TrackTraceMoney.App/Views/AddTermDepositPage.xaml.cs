using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddTermDepositPage : ContentPage
{
    public AddTermDepositPage(AddTermDepositViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
