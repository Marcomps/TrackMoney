using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AccountsListPage : ContentPage
{
    private readonly AccountsListViewModel _viewModel;

    public AccountsListPage(AccountsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadAccountsCommand.Execute(null);
    }
}
