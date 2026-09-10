using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class SnowballPlanPage : ContentPage
{
    private readonly SnowballPlanViewModel _viewModel;

    public SnowballPlanPage(SnowballPlanViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadSnowballPlanCommand.Execute(null);
    }
}
