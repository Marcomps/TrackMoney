using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddTransactionPage : ContentPage
{
    private readonly AddTransactionViewModel _viewModel;

    public AddTransactionPage(AddTransactionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadOptionsCommand.Execute(null);
    }
}
