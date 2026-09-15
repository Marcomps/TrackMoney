using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CloudLoginPage : ContentPage
{
    public CloudLoginPage(CloudLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
