using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CardNetworksListPage : ContentPage
{
    private readonly CardNetworksListViewModel _viewModel;

    public CardNetworksListPage(CardNetworksListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadNetworksCommand.Execute(null);
    }
}
