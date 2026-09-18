using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class ReportsHubPage : ContentPage
{
    public ReportsHubPage(ReportsHubViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
