using CommunityToolkit.Maui;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Services;
using TrackTraceMoney.App.Services.Cloud;
using TrackTraceMoney.App.Services.Profiles;
using TrackTraceMoney.App.ViewModels;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Application.NetWorth;
using TrackTraceMoney.Application.RecurringExpenses;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.TermDeposits;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Infrastructure;
using TrackTraceMoney.Infrastructure.Profiles;

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

		// Registered before AddTrackTraceMoneyProfileCatalog (whose IProfileManagementService
		// registration resolves IActiveProfileStore) and before AddTrackTraceMoneyInfrastructure's
		// now-profile-aware connection-string factory below (same dependency) -- both need it
		// resolvable already. Preferences.Default mirrors the SecureStorage.Default registration a few
		// lines down: wrap a MAUI Essentials static as an injectable singleton, same pattern, just a
		// different Essentials API.
		builder.Services.AddSingleton(Preferences.Default);
		builder.Services.AddSingleton<IActiveProfileStore, PreferencesActiveProfileStore>();

		var profileCatalogDbPath = Path.Combine(FileSystem.AppDataDirectory, "tracktracemoney_profiles.db3");
		builder.Services.AddTrackTraceMoneyProfileCatalog($"Data Source={profileCatalogDbPath}", FileSystem.AppDataDirectory);

		// Legacy, pre-profiles filename. Still the fallback inside the factory below whenever no
		// profile is active yet -- a brand-new install (before CreateFirstProfilePage creates the
		// first profile) or, momentarily, before the one-time existing-install migration further down
		// (which checks this same path) runs.
		var legacyDbPath = Path.Combine(FileSystem.AppDataDirectory, "tracktracemoney.db3");
		builder.Services.AddTrackTraceMoneyInfrastructure(sp =>
		{
			var activeProfileId = sp.GetRequiredService<IActiveProfileStore>().GetActiveProfileIdAsync().GetAwaiter().GetResult();
			var fileName = activeProfileId is { } id ? $"tracktracemoney_{id}.db3" : "tracktracemoney.db3";
			return $"Data Source={Path.Combine(FileSystem.AppDataDirectory, fileName)}";
		});

		builder.Services.AddSingleton(SecureStorage.Default);
		builder.Services.AddHttpClient<ICloudAuthService, CloudAuthService>(client =>
		{
			client.BaseAddress = new Uri(CloudApiConfig.BaseUrl);
			client.Timeout = TimeSpan.FromSeconds(15);
		});
		builder.Services.AddHttpClient<ICloudBackupService, CloudBackupService>(client =>
		{
			client.BaseAddress = new Uri(CloudApiConfig.BaseUrl);
		});

		builder.Services.AddSingleton<IIncomeCalculator, IncomeCalculator>();
		builder.Services.AddSingleton<INetWorthCalculator, NetWorthCalculator>();
		builder.Services.AddScoped<INetWorthSnapshotService, NetWorthSnapshotService>();
		builder.Services.AddScoped<ITermDepositRenewalService, TermDepositRenewalService>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<NetWorthViewModel>();
		builder.Services.AddTransient<NetWorthPage>();

		builder.Services.AddTransient<AccountsListViewModel>();
		builder.Services.AddTransient<AccountsListPage>();
		builder.Services.AddTransient<AddAccountViewModel>();
		builder.Services.AddTransient<AddAccountPage>();
		builder.Services.AddTransient<TermDepositsListViewModel>();
		builder.Services.AddTransient<TermDepositsListPage>();
		builder.Services.AddTransient<AddTermDepositViewModel>();
		builder.Services.AddTransient<AddTermDepositPage>();
		builder.Services.AddTransient<InvestmentFundsListViewModel>();
		builder.Services.AddTransient<InvestmentFundsListPage>();
		builder.Services.AddTransient<AddInvestmentFundViewModel>();
		builder.Services.AddTransient<AddInvestmentFundPage>();
		builder.Services.AddTransient<InvestmentFundDetailViewModel>();
		builder.Services.AddTransient<InvestmentFundDetailPage>();
		builder.Services.AddTransient<RecordInvestmentValuationViewModel>();
		builder.Services.AddTransient<RecordInvestmentValuationPage>();

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
		builder.Services.AddTransient<MedicalExpenseDetailViewModel>();
		builder.Services.AddTransient<MedicalExpenseDetailPage>();

		builder.Services.AddTransient<CategoriesListViewModel>();
		builder.Services.AddTransient<CategoriesListPage>();
		builder.Services.AddTransient<AddCategoryViewModel>();
		builder.Services.AddTransient<AddCategoryPage>();

		builder.Services.AddTransient<PeopleListViewModel>();
		builder.Services.AddTransient<PeopleListPage>();
		builder.Services.AddTransient<AddPersonViewModel>();
		builder.Services.AddTransient<AddPersonPage>();

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

		builder.Services.AddTransient<FinancialInstitutionsListViewModel>();
		builder.Services.AddTransient<FinancialInstitutionsListPage>();
		builder.Services.AddTransient<AddFinancialInstitutionViewModel>();
		builder.Services.AddTransient<AddFinancialInstitutionPage>();
		builder.Services.AddTransient<CardNetworksListViewModel>();
		builder.Services.AddTransient<CardNetworksListPage>();
		builder.Services.AddTransient<AddCardNetworkViewModel>();
		builder.Services.AddTransient<AddCardNetworkPage>();

		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<CloudLoginViewModel>();
		builder.Services.AddTransient<CloudLoginPage>();
		builder.Services.AddTransient<CloudRegisterViewModel>();
		builder.Services.AddTransient<CloudRegisterPage>();

		builder.Services.AddTransient<ProfilesListViewModel>();
		builder.Services.AddTransient<ProfilesListPage>();
		builder.Services.AddTransient<AddProfileViewModel>();
		builder.Services.AddTransient<AddProfilePage>();
		builder.Services.AddTransient<CreateFirstProfileViewModel>();
		builder.Services.AddTransient<CreateFirstProfilePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var services = scope.ServiceProvider;

			// The profile catalog is small and always open regardless of which (if any) finance
			// profile is active, so it migrates unconditionally, first.
			var catalogDbContext = services.GetRequiredService<ProfileCatalogDbContext>();
			catalogDbContext.Database.Migrate();

			var activeProfileStore = services.GetRequiredService<IActiveProfileStore>();
			var activeProfileId = activeProfileStore.GetActiveProfileIdAsync().GetAwaiter().GetResult();

			if (activeProfileId is null && File.Exists(legacyDbPath))
			{
				// An existing, pre-profiles install: silently wrap its one dataset into a new default
				// profile so it keeps working exactly as before, just now as that profile's data.
				var profileManagementService = services.GetRequiredService<IProfileManagementService>();

				// Localized via AppResources.Profiles_MigratedDefaultName, unlike CategorySeeder's
				// default category names -- those resolve through a live culture-aware converter keyed
				// off SystemCategoryKey at render time (the stored literal never reaches the screen),
				// but a LocalProfile's Name has no such converter, so it renders exactly as stored,
				// permanently, everywhere (Settings, the profile switcher). AppResources resolves
				// against CurrentUICulture, which .NET MAUI's platform startup has already established
				// by the time CreateMauiApp() runs this far -- confirmed live, not just assumed.
				var migratedProfile = profileManagementService.CreateProfileAsync(AppResources.Profiles_MigratedDefaultName).GetAwaiter().GetResult();

				// Release any idle pooled native connections before touching the file on disk -- see
				// LocalBackupService's remarks and ProfileManagementService.DeleteProfileDatabaseFiles
				// for the full reasoning (necessary on Windows; purely defensive here, since nothing
				// in this process should have opened the legacy file yet at this point in startup).
				SqliteConnection.ClearAllPools();

				var migratedDbPath = Path.Combine(FileSystem.AppDataDirectory, $"tracktracemoney_{migratedProfile.Id}.db3");
				File.Move(legacyDbPath, migratedDbPath);
				foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
				{
					var sidecar = legacyDbPath + suffix;
					if (File.Exists(sidecar))
					{
						File.Move(sidecar, migratedDbPath + suffix);
					}
				}

				activeProfileStore.SetActiveProfileIdAsync(migratedProfile.Id).GetAwaiter().GetResult();
				activeProfileId = migratedProfile.Id;
			}

			// A genuinely brand-new install (no profile active, no legacy file either) intentionally
			// skips this -- there is no per-profile finance database to migrate/seed yet.
			// CreateFirstProfileViewModel runs the equivalent IFinanceDatabaseInitializer call itself,
			// once the user actually creates their first profile, before it swaps the root page into
			// AppShell (see that class).
			if (activeProfileId is not null)
			{
				var financeDatabaseInitializer = services.GetRequiredService<IFinanceDatabaseInitializer>();
				financeDatabaseInitializer.EnsureReadyAsync(AppResources.PersonRelationshipType_Me).GetAwaiter().GetResult();
			}
		}

		return app;
	}
}
