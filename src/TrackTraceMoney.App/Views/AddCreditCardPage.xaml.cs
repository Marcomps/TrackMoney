using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddCreditCardPage : ContentPage
{
    public AddCreditCardPage(AddCreditCardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
