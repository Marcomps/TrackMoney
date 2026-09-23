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
		Routing.RegisterRoute(nameof(MedicalExpenseDetailPage), typeof(MedicalExpenseDetailPage));
		// Edit/delete slice §4/§5 -- reached only from HistoryPage's OpenTransactionDetailCommand.
		Routing.RegisterRoute(nameof(TransactionDetailPage), typeof(TransactionDetailPage));
		// Categories/People/Budgets/RecurringExpenses are no longer Tabs (see AppShell.xaml's comment) --
		// each List page now needs its own explicit registration here, same as every other pushed
		// (non-Tab) List page below, since a Tab's ShellContent used to register its route implicitly.
		Routing.RegisterRoute(nameof(CategoriesListPage), typeof(CategoriesListPage));
		Routing.RegisterRoute(nameof(AddCategoryPage), typeof(AddCategoryPage));
		Routing.RegisterRoute(nameof(EditCategoryPage), typeof(EditCategoryPage));
		// Transaction Type Customization slice, Half B (§B.4/§B.7.4) -- same pushed-page registration
		// pattern as Categories above.
		Routing.RegisterRoute(nameof(TransactionPresetsListPage), typeof(TransactionPresetsListPage));
		Routing.RegisterRoute(nameof(AddTransactionPresetPage), typeof(AddTransactionPresetPage));
		Routing.RegisterRoute(nameof(EditTransactionPresetPage), typeof(EditTransactionPresetPage));
		Routing.RegisterRoute(nameof(PeopleListPage), typeof(PeopleListPage));
		Routing.RegisterRoute(nameof(AddPersonPage), typeof(AddPersonPage));
		Routing.RegisterRoute(nameof(BudgetsListPage), typeof(BudgetsListPage));
		Routing.RegisterRoute(nameof(AddBudgetPage), typeof(AddBudgetPage));
		Routing.RegisterRoute(nameof(RecurringExpensesListPage), typeof(RecurringExpensesListPage));
		Routing.RegisterRoute(nameof(AddRecurringExpensePage), typeof(AddRecurringExpensePage));
		Routing.RegisterRoute(nameof(RecurringIncomesListPage), typeof(RecurringIncomesListPage));
		Routing.RegisterRoute(nameof(AddRecurringIncomePage), typeof(AddRecurringIncomePage));
		Routing.RegisterRoute(nameof(EditRecurringIncomeAmountPage), typeof(EditRecurringIncomeAmountPage));
		Routing.RegisterRoute(nameof(LoansListPage), typeof(LoansListPage));
		Routing.RegisterRoute(nameof(AddLoanPage), typeof(AddLoanPage));
		Routing.RegisterRoute(nameof(SnowballPlanPage), typeof(SnowballPlanPage));
		Routing.RegisterRoute(nameof(TermDepositsListPage), typeof(TermDepositsListPage));
		Routing.RegisterRoute(nameof(AddTermDepositPage), typeof(AddTermDepositPage));
		Routing.RegisterRoute(nameof(InvestmentFundsListPage), typeof(InvestmentFundsListPage));
		Routing.RegisterRoute(nameof(AddInvestmentFundPage), typeof(AddInvestmentFundPage));
		Routing.RegisterRoute(nameof(InvestmentFundDetailPage), typeof(InvestmentFundDetailPage));
		Routing.RegisterRoute(nameof(RecordInvestmentValuationPage), typeof(RecordInvestmentValuationPage));
		Routing.RegisterRoute(nameof(NetWorthPage), typeof(NetWorthPage));
		Routing.RegisterRoute(nameof(CloudLoginPage), typeof(CloudLoginPage));
		Routing.RegisterRoute(nameof(CloudRegisterPage), typeof(CloudRegisterPage));
		Routing.RegisterRoute(nameof(ProfilesListPage), typeof(ProfilesListPage));
		Routing.RegisterRoute(nameof(AddProfilePage), typeof(AddProfilePage));
		Routing.RegisterRoute(nameof(FinancialInstitutionsListPage), typeof(FinancialInstitutionsListPage));
		Routing.RegisterRoute(nameof(AddFinancialInstitutionPage), typeof(AddFinancialInstitutionPage));
		Routing.RegisterRoute(nameof(CardNetworksListPage), typeof(CardNetworksListPage));
		Routing.RegisterRoute(nameof(AddCardNetworkPage), typeof(AddCardNetworkPage));
		Routing.RegisterRoute(nameof(SetPinPage), typeof(SetPinPage));
		// Reports (README §40 slice 1) -- reachable from Settings, not a Tab (see AppShell.xaml's
		// comment about the 6th-tab "More" overflow bug), same pushed-page registration pattern as
		// Categories/Budgets/RecurringExpenses/People/Profiles/FinancialInstitutions/CardNetworks above.
		Routing.RegisterRoute(nameof(ReportsHubPage), typeof(ReportsHubPage));
		Routing.RegisterRoute(nameof(ExpensesByCategoryReportPage), typeof(ExpensesByCategoryReportPage));
		Routing.RegisterRoute(nameof(IncomeVsExpensesReportPage), typeof(IncomeVsExpensesReportPage));
		Routing.RegisterRoute(nameof(BudgetVsSpendingReportPage), typeof(BudgetVsSpendingReportPage));
		Routing.RegisterRoute(nameof(MonthlyExpensesTrendReportPage), typeof(MonthlyExpensesTrendReportPage));
		// CreateFirstProfilePage/AppLockPage deliberately have no route here -- neither is ever
		// navigated to via Shell, only ever constructed directly as the app's root page (see App.xaml.cs).
	}

	/// <summary>
	/// Switching tabs resets every other tab back to its root page, so returning to a tab (e.g.
	/// Settings after drilling into Manage people) shows its main view rather than whatever pushed
	/// page was left open there. Popped without animation since those tabs aren't visible.
	/// </summary>
	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);

		if (args.Source != ShellNavigationSource.ShellSectionChanged)
			return;

		var currentSection = CurrentItem?.CurrentItem;
		foreach (var section in Items.SelectMany(item => item.Items))
		{
			if (section != currentSection && section.Navigation.NavigationStack.Count > 1)
				_ = section.Navigation.PopToRootAsync(animated: false);
		}
	}
}
