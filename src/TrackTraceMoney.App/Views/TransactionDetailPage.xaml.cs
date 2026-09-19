using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class TransactionDetailPage : ContentPage
{
    private readonly TransactionDetailViewModel _viewModel;

    public TransactionDetailPage(TransactionDetailViewModel viewModel)
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
