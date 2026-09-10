using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddLoanPage : ContentPage
{
    public AddLoanPage(AddLoanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
