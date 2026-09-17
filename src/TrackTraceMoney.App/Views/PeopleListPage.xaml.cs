using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class PeopleListPage : ContentPage
{
    private readonly PeopleListViewModel _viewModel;

    public PeopleListPage(PeopleListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadPeopleCommand.Execute(null);
    }
}
