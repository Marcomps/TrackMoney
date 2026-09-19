using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class EditRecurringIncomeAmountPage : ContentPage
{
    private readonly EditRecurringIncomeAmountViewModel _viewModel;

    public EditRecurringIncomeAmountPage(EditRecurringIncomeAmountViewModel viewModel)
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
