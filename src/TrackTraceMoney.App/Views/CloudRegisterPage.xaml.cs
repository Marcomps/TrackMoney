using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CloudRegisterPage : ContentPage
{
    public CloudRegisterPage(CloudRegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
