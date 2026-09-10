using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrackTraceMoney.App.Services;
using TrackTraceMoney.App.ViewModels;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Application.RecurringExpenses;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Infrastructure;
using TrackTraceMoney.Infrastructure.Persistence;
using TrackTraceMoney.Infrastructure.Seeding;

namespace TrackTraceMoney.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tracktracemoney.db3");
		builder.Services.AddTrackTraceMoneyInfrastructure($"Data Source={dbPath}");

		builder.Services.AddSingleton<IIncomeCalculator, IncomeCalculator>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<DashboardPage>();

		builder.Services.AddTransient<AccountsListViewModel>();
		builder.Services.AddTransient<AccountsListPage>();
		builder.Services.AddTransient<AddAccountViewModel>();
		builder.Services.AddTransient<AddAccountPage>();

		builder.Services.AddTransient<CreditCardsListViewModel>();
		builder.Services.AddTransient<CreditCardsListPage>();
		builder.Services.AddTransient<AddCreditCardViewModel>();
		builder.Services.AddTransient<AddCreditCardPage>();
		builder.Services.AddSingleton<ICreditCardPurchasedVsPaidCalculator, CreditCardPurchasedVsPaidCalculator>();
		builder.Services.AddSingleton<ICreditCardHealthEvaluator, CreditCardHealthEvaluator>();
		builder.Services.AddTransient<CreditCardDetailViewModel>();
		builder.Services.AddTransient<CreditCardDetailPage>();
		builder.Services.AddTransient<RecordCreditCardStatementViewModel>();
		builder.Services.AddTransient<RecordCreditCardStatementPage>();
		builder.Services.AddTransient<LoansListViewModel>();
		builder.Services.AddTransient<LoansListPage>();
		builder.Services.AddTransient<AddLoanViewModel>();
		builder.Services.AddTransient<AddLoanPage>();
		builder.Services.AddSingleton<ISnowballPlanner, SnowballPlanner>();
		builder.Services.AddTransient<SnowballPlanViewModel>();
		builder.Services.AddTransient<SnowballPlanPage>();

		builder.Services.AddSingleton<ILocalNotifier, AndroidLocalNotifier>();
		builder.Services.AddScoped<ITransactionEntryService, TransactionEntryService>();
		builder.Services.AddTransient<TransactionsListViewModel>();
		builder.Services.AddTransient<TransactionsListPage>();
		builder.Services.AddTransient<AddTransactionViewModel>();
		builder.Services.AddTransient<AddTransactionPage>();
		builder.Services.AddTransient<HistoryViewModel>();
		builder.Services.AddTransient<HistoryPage>();

		builder.Services.AddTransient<CategoriesListViewModel>();
		builder.Services.AddTransient<CategoriesListPage>();
		builder.Services.AddTransient<AddCategoryViewModel>();
		builder.Services.AddTransient<AddCategoryPage>();

		builder.Services.AddSingleton<ISpendingCalculator, SpendingCalculator>();
		builder.Services.AddSingleton<IBudgetEvaluator, BudgetEvaluator>();
		builder.Services.AddTransient<BudgetsListViewModel>();
		builder.Services.AddTransient<BudgetsListPage>();
		builder.Services.AddTransient<AddBudgetViewModel>();
		builder.Services.AddTransient<AddBudgetPage>();

		builder.Services.AddScoped<IRecurringExpenseService, RecurringExpenseService>();
		builder.Services.AddTransient<RecurringExpensesListViewModel>();
		builder.Services.AddTransient<RecurringExpensesListPage>();
		builder.Services.AddTransient<AddRecurringExpenseViewModel>();
		builder.Services.AddTransient<AddRecurringExpensePage>();

		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
			dbContext.Database.Migrate();
			CategorySeeder.SeedDefaultCategoriesAsync(dbContext).GetAwaiter().GetResult();
		}

		return app;
	}
}
