using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IActiveProfileStore _activeProfileStore;
	private readonly IAppLockService _appLockService;

	/// <summary>
	/// README §43 app-lock Story 3's auto-lock grace period: in-memory only, by design (see the
	/// app-lock-slice-spec's Decision 4) -- if the OS kills the process entirely while backgrounded,
	/// this field is moot, since a fresh cold start already re-applies CreateWindow's own "is a PIN
	/// set" check below with no extra state needed. Not persisted to Preferences/SecureStorage.
	/// </summary>
	private DateTime? _deactivatedAtUtc;

	/// <summary>
	/// Fixed default for this slice, not user-configurable (app-lock-slice-spec Decision 4) -- long
	/// enough to survive a Settings Export/Restore round trip through
	/// <c>Share.Default.RequestAsync</c>/<c>FilePicker.Default.PickAsync</c> (both legitimately
	/// background this app), short enough that a phone picked up minutes later by someone else is
	/// still locked.
	/// </summary>
	private static readonly TimeSpan AutoLockGracePeriod = TimeSpan.FromSeconds(30);

	public App(IServiceProvider serviceProvider, IActiveProfileStore activeProfileStore, IAppLockService appLockService)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
		_activeProfileStore = activeProfileStore;
		_appLockService = appLockService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Blocking here mirrors MauiProgram.CreateMauiApp's own established precedent
		// (CategorySeeder.SeedDefaultCategoriesAsync(...).GetAwaiter().GetResult()) for bootstrap-time
		// code that must stay synchronous -- CreateWindow has no async override in MAUI.
		// Guarded (found via checkpoint code review): this runs before any page exists, so an unhandled
		// exception here (e.g. a corrupted Preferences store on some device/first-run edge case) would
		// crash the app before the user ever sees a single screen -- strictly worse than occasionally
		// skipping the lock screen, which still leaves the app fully usable. Fails open, never crashed.
		bool isAppLockEnabled;
		try
		{
			isAppLockEnabled = _appLockService.IsEnabledAsync().GetAwaiter().GetResult();
		}
		catch
		{
			isAppLockEnabled = false;
		}

		// README §43 app-lock Story 2: a prior gate layered in front of the existing
		// CreateFirstProfilePage-vs-AppShell branch below, not a replacement for it -- if no PIN is
		// set (the common case for every install before Settings' "App Lock" toggle is ever used),
		// this branch is skipped entirely and behavior is byte-for-byte unchanged from before this
		// slice (Story 2's explicit no-regression acceptance criterion).
		Page rootPage = isAppLockEnabled
			? _serviceProvider.GetRequiredService<AppLockPage>()
			: ResolveUnlockedRootPage();

		var window = new Window(rootPage);
		window.Activated += OnWindowActivated;
		window.Deactivated += OnWindowDeactivated;
		return window;
	}

	/// <summary>
	/// The pre-app-lock root-page decision (unchanged from before this slice) -- also re-run verbatim
	/// by <c>AppLockViewModel.SubmitAsync</c> on a correct PIN, since app-lock must re-derive this
	/// exact destination rather than hardcoding <see cref="AppShell"/> (the last profile could have
	/// been deleted while the device sat locked).
	/// </summary>
	private Page ResolveUnlockedRootPage()
	{
		var activeProfileId = _activeProfileStore.GetActiveProfileIdAsync().GetAwaiter().GetResult();

		// No profile active yet -- either a genuinely brand-new install, or an existing install whose
		// legacy-data migration (see MauiProgram.cs) found nothing to migrate. Either way,
		// CreateFirstProfilePage collects a name and creates/activates the first profile itself.
		return activeProfileId is null
			? _serviceProvider.GetRequiredService<CreateFirstProfilePage>()
			: new AppShell();
	}

	private void OnWindowDeactivated(object? sender, EventArgs e) => _deactivatedAtUtc = DateTime.UtcNow;

	private void OnWindowActivated(object? sender, EventArgs e)
	{
		if (_deactivatedAtUtc is not { } deactivatedAtUtc)
			return; // First activation (initial launch) -- nothing to compare against.

		var backgroundedFor = DateTime.UtcNow - deactivatedAtUtc;
		_deactivatedAtUtc = null;

		// Within the grace period -- e.g. a Share-sheet/FilePicker round trip from Settings' existing
		// Export/Restore actions, which legitimately background this app as part of an
		// already-in-progress in-app action. Do not re-lock (Story 3's explicit regression case).
		if (backgroundedFor <= AutoLockGracePeriod)
			return;

		if (!_appLockService.IsEnabledAsync().GetAwaiter().GetResult())
			return;

		if (sender is not Window window)
			return;

		// A fresh AppLockViewModel every time (AddTransient in MauiProgram.cs) -- failed-attempt count
		// and cooldown state must not survive from a previous lock, and the page shown here is the
		// lock screen itself, never the page the user was previously on (Story 3's own wording).
		window.Page = _serviceProvider.GetRequiredService<AppLockPage>();
	}
}
