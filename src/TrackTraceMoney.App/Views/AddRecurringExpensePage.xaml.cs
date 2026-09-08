using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class AddRecurringExpensePage : ContentPage
{
    private readonly AddRecurringExpenseViewModel _viewModel;

    public AddRecurringExpensePage(AddRecurringExpenseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadOptionsCommand.Execute(null);
    }
}
