using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class CategoriesListPage : ContentPage
{
    private readonly CategoriesListViewModel _viewModel;

    public CategoriesListPage(CategoriesListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCategoriesCommand.Execute(null);
    }
}
