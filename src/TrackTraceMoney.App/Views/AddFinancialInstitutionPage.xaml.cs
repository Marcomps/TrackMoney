using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddFinancialInstitutionPage : ContentPage
{
    public AddFinancialInstitutionPage(AddFinancialInstitutionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
