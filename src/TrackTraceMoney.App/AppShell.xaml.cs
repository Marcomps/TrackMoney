using TrackTraceMoney.App.Views;

namespace TrackTraceMoney.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(AddAccountPage), typeof(AddAccountPage));
		Routing.RegisterRoute(nameof(AddCreditCardPage), typeof(AddCreditCardPage));
		Routing.RegisterRoute(nameof(CreditCardDetailPage), typeof(CreditCardDetailPage));
		Routing.RegisterRoute(nameof(RecordCreditCardStatementPage), typeof(RecordCreditCardStatementPage));
		Routing.RegisterRoute(nameof(AddTransactionPage), typeof(AddTransactionPage));
		Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
		Routing.RegisterRoute(nameof(AddCategoryPage), typeof(AddCategoryPage));
		Routing.RegisterRoute(nameof(AddBudgetPage), typeof(AddBudgetPage));
		Routing.RegisterRoute(nameof(AddRecurringExpensePage), typeof(AddRecurringExpensePage));
		Routing.RegisterRoute(nameof(LoansListPage), typeof(LoansListPage));
		Routing.RegisterRoute(nameof(AddLoanPage), typeof(AddLoanPage));
		Routing.RegisterRoute(nameof(SnowballPlanPage), typeof(SnowballPlanPage));
	}
}
