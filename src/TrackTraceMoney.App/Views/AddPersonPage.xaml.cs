using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddPersonPage : ContentPage
{
    public AddPersonPage(AddPersonViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
