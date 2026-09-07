using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class TransactionsListPage : ContentPage
{
    private readonly TransactionsListViewModel _viewModel;

    public TransactionsListPage(TransactionsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadTransactionsCommand.Execute(null);
    }
}
