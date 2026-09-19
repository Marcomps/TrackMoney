using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class RecurringIncomesListPage : ContentPage
{
    private readonly RecurringIncomesListViewModel _viewModel;

    public RecurringIncomesListPage(RecurringIncomesListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadRecurringIncomesCommand.Execute(null);
    }
}
