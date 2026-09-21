using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddTransactionPresetPage : ContentPage
{
    private readonly AddTransactionPresetViewModel _viewModel;

    public AddTransactionPresetPage(AddTransactionPresetViewModel viewModel)
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
