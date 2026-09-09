using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CreditCardsListPage : ContentPage
{
    private readonly CreditCardsListViewModel _viewModel;

    public CreditCardsListPage(CreditCardsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCreditCardsCommand.Execute(null);
    }
}
