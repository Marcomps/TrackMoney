using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CreateFirstProfilePage : ContentPage
{
    public CreateFirstProfilePage(CreateFirstProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
