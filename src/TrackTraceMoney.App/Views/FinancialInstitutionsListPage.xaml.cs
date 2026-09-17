using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class FinancialInstitutionsListPage : ContentPage
{
    private readonly FinancialInstitutionsListViewModel _viewModel;

    public FinancialInstitutionsListPage(FinancialInstitutionsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadInstitutionsCommand.Execute(null);
    }
}
