using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class LoansListPage : ContentPage
{
    private readonly LoansListViewModel _viewModel;

    public LoansListPage(LoansListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadLoansCommand.Execute(null);
    }
}
