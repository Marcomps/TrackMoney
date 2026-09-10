using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class TermDepositsListPage : ContentPage
{
    private readonly TermDepositsListViewModel _viewModel;

    public TermDepositsListPage(TermDepositsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadTermDepositsCommand.Execute(null);
    }
}
