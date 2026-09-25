using System.ComponentModel;
using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CreditCardsListPage : ContentPage
{
    private readonly CreditCardsListViewModel _viewModel;
    private readonly LoansListViewModel _loans;

    public CreditCardsListPage(CreditCardsListViewModel viewModel, LoansListViewModel loans)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _loans = loans;
        BindingContext = viewModel;
        LoansSection.BindingContext = loans;

        // One "show inactive" switch for the whole tab: mirror it onto the loans section.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCreditCardsCommand.Execute(null);
        _loans.LoadLoansCommand.Execute(null);
    }

    // The RefreshView's own Command reloads the cards; this reloads the loans section with it.
    private void OnRefreshing(object? sender, EventArgs e) => _loans.LoadLoansCommand.Execute(null);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreditCardsListViewModel.ShowInactive))
            _loans.ShowInactive = _viewModel.ShowInactive;
    }
}
