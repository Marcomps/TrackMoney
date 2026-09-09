using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CreditCardDetailPage : ContentPage
{
    private readonly CreditCardDetailViewModel _viewModel;

    public CreditCardDetailPage(CreditCardDetailViewModel viewModel)
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
