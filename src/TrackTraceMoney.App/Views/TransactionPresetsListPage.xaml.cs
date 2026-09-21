using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class TransactionPresetsListPage : ContentPage
{
    private readonly TransactionPresetsListViewModel _viewModel;

    public TransactionPresetsListPage(TransactionPresetsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadPresetsCommand.Execute(null);
    }
}
