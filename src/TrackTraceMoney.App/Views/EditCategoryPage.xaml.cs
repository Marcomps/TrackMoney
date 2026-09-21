using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class EditCategoryPage : ContentPage
{
    private readonly EditCategoryViewModel _viewModel;

    public EditCategoryPage(EditCategoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
