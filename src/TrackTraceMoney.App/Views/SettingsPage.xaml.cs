using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshCloudAccountStateCommand.Execute(null);
        _viewModel.RefreshProfileSummaryCommand.Execute(null);
        _viewModel.RefreshAppLockStateCommand.Execute(null);
    }

    // Not a plain two-way bind reacted to via a ViewModel property setter -- RequestAppLockToggleCommand
    // needs the requested new value (e.Value) to decide whether to navigate to SetPinPage or prompt for
    // the current PIN, and reverts IsAppLockEnabled itself either way (see that command's own remarks),
    // so this handler only needs to forward the raw event, nothing else.
    private void OnAppLockToggled(object? sender, ToggledEventArgs e)
    {
        _viewModel.RequestAppLockToggleCommand.Execute(e.Value);
    }
}
