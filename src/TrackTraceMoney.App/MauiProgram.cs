using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrackTraceMoney.App.ViewModels;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Budgets;
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

		builder.Services.AddTransient<AccountsListViewModel>();
		builder.Services.AddTransient<AccountsListPage>();
		builder.Services.AddTransient<AddAccountViewModel>();
		builder.Services.AddTransient<AddAccountPage>();

		builder.Services.AddScoped<ITransactionEntryService, TransactionEntryService>();
		builder.Services.AddTransient<TransactionsListViewModel>();
		builder.Services.AddTransient<TransactionsListPage>();
		builder.Services.AddTransient<AddTransactionViewModel>();
		builder.Services.AddTransient<AddTransactionPage>();

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
