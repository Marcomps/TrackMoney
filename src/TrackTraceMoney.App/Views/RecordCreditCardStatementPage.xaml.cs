using TrackTraceMoney.App.ViewModels;

namespace TrackTraceMoney.App.Views;

public partial class RecordCreditCardStatementPage : ContentPage
{
    private readonly RecordCreditCardStatementViewModel _viewModel;

    public RecordCreditCardStatementPage(RecordCreditCardStatementViewModel viewModel)
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
