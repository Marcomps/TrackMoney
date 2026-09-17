using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddCardNetworkPage : ContentPage
{
    public AddCardNetworkPage(AddCardNetworkViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
