using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AccountsListPage : ContentPage
{
    private readonly AccountsListViewModel _viewModel;
    private readonly TermDepositsListViewModel _termDeposits;
    private readonly InvestmentFundsListViewModel _investmentFunds;

    public AccountsListPage(
        AccountsListViewModel viewModel,
        TermDepositsListViewModel termDeposits,
        InvestmentFundsListViewModel investmentFunds)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _termDeposits = termDeposits;
        _investmentFunds = investmentFunds;
        BindingContext = viewModel;
        TermDepositsSection.BindingContext = termDeposits;
        InvestmentFundsSection.BindingContext = investmentFunds;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadAccountsCommand.Execute(null);
        LoadSections();
    }

    // The RefreshView's own Command reloads the accounts; this reloads the other two sections with it.
    private void OnRefreshing(object? sender, EventArgs e) => LoadSections();

    private void LoadSections()
    {
        _termDeposits.LoadTermDepositsCommand.Execute(null);
        _investmentFunds.LoadInvestmentFundsCommand.Execute(null);
    }
}
