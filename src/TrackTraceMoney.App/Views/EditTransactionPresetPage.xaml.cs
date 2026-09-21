using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class EditTransactionPresetPage : ContentPage
{
    private readonly EditTransactionPresetViewModel _viewModel;

    public EditTransactionPresetPage(EditTransactionPresetViewModel viewModel)
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
