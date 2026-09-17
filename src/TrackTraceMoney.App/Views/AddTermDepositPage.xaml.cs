using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddTermDepositPage : ContentPage
{
    private readonly AddTermDepositViewModel _viewModel;

    public AddTermDepositPage(AddTermDepositViewModel viewModel)
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
