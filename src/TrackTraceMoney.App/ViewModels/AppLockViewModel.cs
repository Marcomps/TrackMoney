using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Backs <see cref="Views.AppLockPage"/> — the app-lock lock screen (README §43). Constructed
/// directly by <c>App.xaml.cs</c> as the root page (cold start with a PIN set) or as a live root-page
/// swap (auto-lock on resume past the grace period), never reached via a Shell route — mirrors
/// <c>CreateFirstProfilePage</c>'s existing precedent for a pre-Shell root page.
/// </summary>
public sealed partial class AppLockViewModel : ObservableObject
{
    /// <summary>app-lock-slice-spec Decision 6: a minimal cooldown, not full lockout/wipe.</summary>
    private const int MaxAttemptsBeforeCooldown = 5;

    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(30);

    private readonly IAppLockService _appLockService;
    private readonly IActiveProfileStore _activeProfileStore;
    private readonly IServiceProvider _serviceProvider;

    private int _failedAttempts;
    private IDispatcherTimer? _cooldownTimer;

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isCooldownActive;

    [ObservableProperty]
    private int cooldownSecondsRemaining;

    public string CooldownMessage => string.Format(AppResources.AppLock_CooldownMessage, CooldownSecondsRemaining);

    /// <summary>Mirrors <c>SettingsViewModel.IsCloudUnauthenticated</c>'s established computed-inverse-bool pattern (no generic value converter exists in this codebase).</summary>
    public bool IsNotCooldownActive => !IsCooldownActive;

    partial void OnCooldownSecondsRemainingChanged(int value) => OnPropertyChanged(nameof(CooldownMessage));

    partial void OnIsCooldownActiveChanged(bool value) => OnPropertyChanged(nameof(IsNotCooldownActive));

    public AppLockViewModel(IAppLockService appLockService, IActiveProfileStore activeProfileStore, IServiceProvider serviceProvider)
    {
        _appLockService = appLockService;
        _activeProfileStore = activeProfileStore;
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsCooldownActive)
            return;

        ErrorMessage = null;

        if (Pin.Length != 6 || !Pin.All(char.IsDigit))
        {
            ErrorMessage = AppResources.AppLock_ValidationPinInvalid;
            Pin = string.Empty;
            return;
        }

        var isCorrect = await _appLockService.VerifyPinAsync(Pin);
        Pin = string.Empty; // Never let the entered PIN linger, correct or not.

        if (!isCorrect)
        {
            _failedAttempts++;
            if (_failedAttempts >= MaxAttemptsBeforeCooldown)
                StartCooldown();
            else
                ErrorMessage = AppResources.AppLock_ValidationWrongPin;
            return;
        }

        _failedAttempts = 0;

        // Must re-derive the destination exactly as App.CreateWindow's own unlocked-path branch does
        // (see that file) -- app-lock is a prior gate layered in front of the existing
        // CreateFirstProfilePage-vs-AppShell branch, not a replacement for it (e.g. the last profile
        // could have been deleted while the device sat locked).
        var activeProfileId = await _activeProfileStore.GetActiveProfileIdAsync();
        Page destination = activeProfileId is null
            ? _serviceProvider.GetRequiredService<CreateFirstProfilePage>()
            : new AppShell();

        // Fully qualified for the same reason CreateFirstProfileViewModel.SaveAsync's own swap is --
        // this namespace chain (TrackTraceMoney.App.ViewModels -> TrackTraceMoney.App ->
        // TrackTraceMoney) also contains the sibling TrackTraceMoney.Application project's namespace,
        // which shadows the bare "Application" MAUI type.
        Microsoft.Maui.Controls.Application.Current!.Windows[0].Page = destination;
    }

    private void StartCooldown()
    {
        IsCooldownActive = true;
        CooldownSecondsRemaining = (int)CooldownDuration.TotalSeconds;
        ErrorMessage = null;

        _cooldownTimer = Microsoft.Maui.Controls.Application.Current!.Dispatcher.CreateTimer();
        _cooldownTimer.Interval = TimeSpan.FromSeconds(1);
        _cooldownTimer.Tick += OnCooldownTick;
        _cooldownTimer.Start();
    }

    private void OnCooldownTick(object? sender, EventArgs e)
    {
        CooldownSecondsRemaining--;
        if (CooldownSecondsRemaining > 0)
            return;

        if (_cooldownTimer is not null)
        {
            _cooldownTimer.Stop();
            // Explicit unsubscribe, not just Stop(): this VM is AddTransient, and Tick was still
            // attached, so leaving it attached risks the timer (and its captured "this") outliving this
            // VM instance for up to the remaining cooldown duration if a new lock screen supersedes it
            // first (found via checkpoint code review).
            _cooldownTimer.Tick -= OnCooldownTick;
            _cooldownTimer = null;
        }
        IsCooldownActive = false;
        _failedAttempts = 0;
    }
}
