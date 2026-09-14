using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class MedicalExpenseDetailPage : ContentPage
{
    private readonly MedicalExpenseDetailViewModel _viewModel;

    public MedicalExpenseDetailPage(MedicalExpenseDetailViewModel viewModel)
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
