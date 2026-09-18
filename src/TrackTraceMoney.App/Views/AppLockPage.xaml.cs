using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AppLockPage : ContentPage
{
    public AppLockPage(AppLockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
