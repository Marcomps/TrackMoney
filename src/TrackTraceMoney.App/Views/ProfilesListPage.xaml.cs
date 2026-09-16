using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class ProfilesListPage : ContentPage
{
    private readonly ProfilesListViewModel _viewModel;

    public ProfilesListPage(ProfilesListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadProfilesCommand.Execute(null);
    }
}
