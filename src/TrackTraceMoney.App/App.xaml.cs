using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IActiveProfileStore _activeProfileStore;

	public App(IServiceProvider serviceProvider, IActiveProfileStore activeProfileStore)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
		_activeProfileStore = activeProfileStore;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Blocking here mirrors MauiProgram.CreateMauiApp's own established precedent
		// (CategorySeeder.SeedDefaultCategoriesAsync(...).GetAwaiter().GetResult()) for bootstrap-time
		// code that must stay synchronous -- CreateWindow has no async override in MAUI.
		var activeProfileId = _activeProfileStore.GetActiveProfileIdAsync().GetAwaiter().GetResult();

		// No profile active yet -- either a genuinely brand-new install, or an existing install whose
		// legacy-data migration (see MauiProgram.cs) found nothing to migrate. Either way,
		// CreateFirstProfilePage collects a name and creates/activates the first profile itself.
		Page rootPage = activeProfileId is null
			? _serviceProvider.GetRequiredService<CreateFirstProfilePage>()
			: new AppShell();

		return new Window(rootPage);
	}
}
