using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class RecordInvestmentValuationPage : ContentPage
{
    private readonly RecordInvestmentValuationViewModel _viewModel;

    public RecordInvestmentValuationPage(RecordInvestmentValuationViewModel viewModel)
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
