using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddCategoryPage : ContentPage
{
    public AddCategoryPage(AddCategoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
