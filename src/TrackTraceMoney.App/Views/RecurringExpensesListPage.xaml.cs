using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class RecurringExpensesListPage : ContentPage
{
    private readonly RecurringExpensesListViewModel _viewModel;

    public RecurringExpensesListPage(RecurringExpensesListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadRecurringExpensesCommand.Execute(null);
    }
}
