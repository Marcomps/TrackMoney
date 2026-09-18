using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class SetPinPage : ContentPage
{
    public SetPinPage(SetPinViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
