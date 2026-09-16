using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddProfilePage : ContentPage
{
    public AddProfilePage(AddProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
