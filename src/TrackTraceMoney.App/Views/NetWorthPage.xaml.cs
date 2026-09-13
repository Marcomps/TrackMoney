using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class NetWorthPage : ContentPage
{
    private readonly NetWorthViewModel _viewModel;

    public NetWorthPage(NetWorthViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
