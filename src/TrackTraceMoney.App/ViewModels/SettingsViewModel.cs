using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Services.Cloud;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalBackupService _backupService;
    private readonly ICloudAuthService _cloudAuthService;
    private readonly ICloudBackupService _cloudBackupService;
    private readonly IActiveProfileStore _activeProfileStore;
    private readonly ILocalProfileRepository _profileRepository;
    private readonly IAppLockService _appLockService;
    private bool _suppressAppLockToggleCommand;

    [ObservableProperty]
    private string? activeProfileName;

    [ObservableProperty]
    private bool isAppLockEnabled;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isCloudAuthenticated;

    [ObservableProperty]
    private string? cloudAccountEmail;

    [ObservableProperty]
    private bool cloudBackupIsBusy;

    [ObservableProperty]
    private string? cloudBackupErrorMessage;

    [ObservableProperty]
    private string? cloudBackupStatusMessage;

    [ObservableProperty]
    private bool cloudBackupExists;

    [ObservableProperty]
    private DateTimeOffset? lastCloudBackupAtUtc;

    public SettingsViewModel(
        ILocalBackupService backupService,
        ICloudAuthService cloudAuthService,
        ICloudBackupService cloudBackupService,
        IActiveProfileStore activeProfileStore,
        ILocalProfileRepository profileRepository,
        IAppLockService appLockService)
    {
        _backupService = backupService;
        _cloudAuthService = cloudAuthService;
        _cloudBackupService = cloudBackupService;
        _activeProfileStore = activeProfileStore;
        _profileRepository = profileRepository;
        _appLockService = appLockService;
    }

    public bool IsCloudUnauthenticated => !IsCloudAuthenticated;

    public string CloudAccountStatusText => string.Format(AppResources.CloudAccount_LoggedInAs, CloudAccountEmail);

    public string LastCloudBackupText => LastCloudBackupAtUtc.HasValue
        ? string.Format(AppResources.CloudBackup_LastBackupKnown, LastCloudBackupAtUtc)
        : AppResources.CloudBackup_LastBackupNone;

    public string ActiveProfileLabelText => string.Format(AppResources.Profiles_ActiveProfileLabel, ActiveProfileName);

    // A backup and a restore must never run concurrently against the same server-side row (see
    // finding #5/#3 of the Phase 4 checkpoint review) — both buttons gate on busy state, and
    // Restore additionally requires a known backup to exist.
    public bool CanTriggerCloudBackupOperation => !CloudBackupIsBusy;

    public bool CanRestoreFromCloud => CloudBackupExists && !CloudBackupIsBusy;

    partial void OnIsCloudAuthenticatedChanged(bool value) => OnPropertyChanged(nameof(IsCloudUnauthenticated));

    partial void OnCloudAccountEmailChanged(string? value) => OnPropertyChanged(nameof(CloudAccountStatusText));

    partial void OnActiveProfileNameChanged(string? value) => OnPropertyChanged(nameof(ActiveProfileLabelText));

    partial void OnLastCloudBackupAtUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastCloudBackupText));

    partial void OnCloudBackupIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTriggerCloudBackupOperation));
        OnPropertyChanged(nameof(CanRestoreFromCloud));
    }

    partial void OnCloudBackupExistsChanged(bool value) => OnPropertyChanged(nameof(CanRestoreFromCloud));

    [RelayCommand]
    private async Task RefreshProfileSummaryAsync()
    {
        var activeProfileId = await _activeProfileStore.GetActiveProfileIdAsync();
        if (activeProfileId is null)
        {
            ActiveProfileName = null;
            return;
        }

        var profile = await _profileRepository.GetByIdAsync(activeProfileId.Value);
        ActiveProfileName = profile?.Name;
    }

    [RelayCommand]
    private static async Task GoToManageProfilesAsync()
    {
        await Shell.Current.GoToAsync(nameof(ProfilesListPage));
    }

    [RelayCommand]
    private async Task RefreshAppLockStateAsync()
    {
        // Guarded like RequestAppLockToggleAsync below: assigning IsAppLockEnabled re-fires the bound
        // Switch's Toggled event, and loading the real persisted state on page appear is not a user
        // toggle request.
        if (_suppressAppLockToggleCommand)
            return;

        _suppressAppLockToggleCommand = true;
        try
        {
            IsAppLockEnabled = await _appLockService.IsEnabledAsync();
        }
        finally
        {
            // Deferred clear, not a bare assignment -- see RequestAppLockToggleAsync's finally for why:
            // this method's own property assignment above has no await after it either, so a
            // synchronous clear here would have the identical race.
            MainThread.BeginInvokeOnMainThread(() => _suppressAppLockToggleCommand = false);
        }
    }

    /// <summary>
    /// README §43 app-lock Story 1. Not a bare two-way-bound switch: <see cref="IsAppLockEnabled"/> is
    /// reverted immediately in both directions below, since nothing actually changes until either
    /// <c>SetPinPage</c> saves a new PIN (turning on) or the current PIN is verified (turning off) --
    /// <see cref="Views.SettingsPage"/>'s own <c>OnAppearing</c> re-runs <see cref="RefreshAppLockStateAsync"/>
    /// afterward, so the toggle always ends up reflecting the real persisted state, not an
    /// optimistically-flipped one.
    /// <para>
    /// The whole method body runs under <c>_suppressAppLockToggleCommand</c>, released only in a
    /// <c>finally</c> once every await here has completed -- not just around the property assignment.
    /// <c>IsAppLockEnabled</c> is two-way bound to the Switch's <c>IsToggled</c>, and MAUI's Switch
    /// raises <c>Toggled</c> for a programmatic change exactly like a user tap; that re-entrant fire
    /// was observed landing on a later main-thread dispatch, not synchronously within the property
    /// assignment, so a guard that cleared immediately after the assignment (tried first) did not
    /// actually block it. Without the guard held for the full method, the re-entrant call reverts the
    /// opposite way, re-triggering Toggled again -- an infinite ping-pong that, on the disable branch,
    /// stacks a new native <c>Shell.Current.DisplayPromptAsync</c> dialog on every iteration. Found
    /// live: a single tap exhausted the emulator's window/memory budget (900+ leaked "Disable app
    /// lock" dialog windows within seconds) and froze the whole device, not just this app.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task RequestAppLockToggleAsync(bool requestedEnabled)
    {
        if (_suppressAppLockToggleCommand)
            return;

        _suppressAppLockToggleCommand = true;
        try
        {
            ErrorMessage = null;
            StatusMessage = null;

            if (requestedEnabled)
            {
                IsAppLockEnabled = false;
                await Shell.Current.GoToAsync(nameof(SetPinPage));
                return;
            }

            // Turning off requires re-entering the current PIN first (Story 1's explicit acceptance
            // criterion) -- otherwise anyone with the phone already unlocked could silently disable
            // protection with one tap.
            IsAppLockEnabled = true;

            var enteredPin = await Shell.Current.DisplayPromptAsync(
                AppResources.AppLock_DisablePromptTitle,
                AppResources.AppLock_DisablePromptMessage,
                AppResources.AppLock_DisablePromptAccept,
                AppResources.AppLock_DisablePromptCancel,
                keyboard: Keyboard.Numeric,
                maxLength: 6);

            if (string.IsNullOrEmpty(enteredPin))
                return;

            if (!await _appLockService.VerifyPinAsync(enteredPin))
            {
                ErrorMessage = AppResources.AppLock_DisableWrongPin;
                return;
            }

            await _appLockService.DisableAsync();
            IsAppLockEnabled = false;
            StatusMessage = AppResources.AppLock_DisabledSuccess;
        }
        finally
        {
            // Not a bare synchronous clear: a post-Phase-4-style checkpoint code review caught a real
            // gap this left open. The disable-success branch's final "IsAppLockEnabled = false" has no
            // await after it, unlike every other branch here -- so a synchronous clear would race the
            // deferred Toggled re-fire that assignment triggers (same MAUI Switch behavior documented
            // on this method above) and lose: the flag clears, THEN the deferred Toggled dispatch
            // arrives unguarded, re-entering this command with the stale value and popping a second,
            // spurious "enter PIN to disable" dialog against a PIN that DisableAsync already erased.
            // Posting the clear itself onto the main-thread queue -- instead of running it inline --
            // guarantees it lands after any Toggled dispatch queued earlier in this same method call,
            // since both share one FIFO main-thread queue. Applied to every exit path uniformly rather
            // than auditing each branch for "is there an await after the last mutation," so this can't
            // regress the same way if a future edit adds or reorders a branch.
            MainThread.BeginInvokeOnMainThread(() => _suppressAppLockToggleCommand = false);
        }
    }

    [RelayCommand]
    private static async Task GoToManageFinancialInstitutionsAsync()
    {
        await Shell.Current.GoToAsync(nameof(FinancialInstitutionsListPage));
    }

    [RelayCommand]
    private static async Task GoToManageCardNetworksAsync()
    {
        await Shell.Current.GoToAsync(nameof(CardNetworksListPage));
    }

    // Categories/Budgets/RecurringExpenses/People moved here from the TabBar (see AppShell.xaml's
    // comment) — occasional "manage/configure" screens, not daily-driver tabs, same reachability
    // pattern as Profiles/FinancialInstitutions/CardNetworks above.
    [RelayCommand]
    private static async Task GoToManageCategoriesAsync()
    {
        await Shell.Current.GoToAsync(nameof(CategoriesListPage));
    }

    [RelayCommand]
    private static async Task GoToManageBudgetsAsync()
    {
        await Shell.Current.GoToAsync(nameof(BudgetsListPage));
    }

    [RelayCommand]
    private static async Task GoToManageRecurringExpensesAsync()
    {
        await Shell.Current.GoToAsync(nameof(RecurringExpensesListPage));
    }

    [RelayCommand]
    private static async Task GoToManageRecurringIncomeAsync()
    {
        await Shell.Current.GoToAsync(nameof(RecurringIncomesListPage));
    }

    [RelayCommand]
    private static async Task GoToManagePeopleAsync()
    {
        await Shell.Current.GoToAsync(nameof(PeopleListPage));
    }

    // Reports (README §40 slice 1) live here, not as a 6th Tab: Android's Material bottom-nav
    // auto-collapses a 6th+ tab into a native "More" sheet that ignores this app's dark theme -- a bug
    // already found and fixed once for Categories/Budgets/RecurringExpenses/People (see AppShell.xaml's
    // comment). Same reachability pattern as those.
    [RelayCommand]
    private static async Task GoToManageReportsAsync()
    {
        await Shell.Current.GoToAsync(nameof(ReportsHubPage));
    }

    [RelayCommand]
    private async Task RefreshCloudAccountStateAsync()
    {
        IsCloudAuthenticated = await _cloudAuthService.IsAuthenticatedAsync();
        CloudAccountEmail = IsCloudAuthenticated ? await _cloudAuthService.GetCurrentEmailAsync() : null;

        if (IsCloudAuthenticated)
        {
            await RefreshCloudBackupStatusAsync();
        }
        else
        {
            CloudBackupExists = false;
            LastCloudBackupAtUtc = null;
        }
    }

    [RelayCommand]
    private async Task RefreshCloudBackupStatusAsync()
    {
        try
        {
            var result = await _cloudBackupService.GetStatusAsync();
            if (result.Success)
            {
                CloudBackupExists = result.Exists;
                LastCloudBackupAtUtc = result.LastBackupAtUtc;
            }
            else if (result.Error == CloudBackupResultError.NotAuthenticated)
            {
                // Self-heal: this method is called from RefreshCloudAccountStateAsync, so calling
                // it back here would recurse; setting the flags directly mirrors its else-branch.
                IsCloudAuthenticated = false;
                CloudAccountEmail = null;
                CloudBackupExists = false;
                LastCloudBackupAtUtc = null;
            }
            else
            {
                // A logged-out status check failing isn't noteworthy; anything else is.
                CloudBackupErrorMessage = AppResources.CloudBackup_StatusError;
            }
        }
        catch (Exception)
        {
            CloudBackupErrorMessage = AppResources.CloudBackup_StatusError;
        }
    }

    [RelayCommand]
    private async Task BackupToCloudAsync()
    {
        CloudBackupIsBusy = true;
        CloudBackupErrorMessage = null;
        CloudBackupStatusMessage = null;
        try
        {
            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"cloud-backup-{Guid.NewGuid():N}.db3");
            try
            {
                await _backupService.ExportAsync(tempPath);
                var result = await _cloudBackupService.UploadAsync(tempPath);
                if (result.Success)
                {
                    CloudBackupExists = true;
                    LastCloudBackupAtUtc = result.LastBackupAtUtc;
                    CloudBackupStatusMessage = AppResources.CloudBackup_UploadSuccess;
                }
                else
                {
                    CloudBackupErrorMessage = MapBackupError(result.Error!.Value, isRestore: false);
                    if (result.Error == CloudBackupResultError.NotAuthenticated)
                        await RefreshCloudAccountStateAsync();
                }
            }
            catch (Exception)
            {
                CloudBackupErrorMessage = AppResources.CloudBackup_UploadError;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        finally
        {
            CloudBackupIsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreFromCloudAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.CloudBackup_RestoreConfirmTitle,
            AppResources.CloudBackup_RestoreConfirmMessage,
            AppResources.CloudBackup_RestoreConfirmAccept,
            AppResources.CloudBackup_RestoreConfirmCancel);

        if (!confirmed)
            return;

        CloudBackupIsBusy = true;
        CloudBackupErrorMessage = null;
        CloudBackupStatusMessage = null;
        try
        {
            var result = await _cloudBackupService.DownloadAsync();
            if (result.Success)
            {
                await using var stream = result.Data!;
                await _backupService.RestoreAsync(stream);
                CloudBackupStatusMessage = AppResources.CloudBackup_RestoreSuccess;
            }
            else
            {
                CloudBackupErrorMessage = MapBackupError(result.Error!.Value, isRestore: true);
                if (result.Error == CloudBackupResultError.NotAuthenticated)
                    await RefreshCloudAccountStateAsync();
            }
        }
        catch (Exception)
        {
            CloudBackupErrorMessage = AppResources.CloudBackup_RestoreError;
        }
        finally
        {
            CloudBackupIsBusy = false;
        }
    }

    private static string MapBackupError(CloudBackupResultError error, bool isRestore) => error switch
    {
        CloudBackupResultError.NotAuthenticated => AppResources.CloudBackup_NotAuthenticatedError,
        CloudBackupResultError.NetworkUnavailable => AppResources.CloudBackup_NetworkError,
        CloudBackupResultError.NoBackupFound when isRestore => AppResources.CloudBackup_NoBackupFoundError,
        _ => isRestore ? AppResources.CloudBackup_RestoreError : AppResources.CloudBackup_UploadError
    };

    [RelayCommand]
    private static async Task GoToCloudLoginAsync()
    {
        await Shell.Current.GoToAsync(nameof(CloudLoginPage));
    }

    [RelayCommand]
    private static async Task GoToCloudRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(CloudRegisterPage));
    }

    [RelayCommand]
    private async Task CloudLogoutAsync()
    {
        await _cloudAuthService.LogoutAsync();
        await RefreshCloudAccountStateAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        IsBusy = true;
        try
        {
            var fileName = $"tracktracemoney-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db3";
            var exportPath = Path.Combine(FileSystem.CacheDirectory, fileName);

            await _backupService.ExportAsync(exportPath);

            // Stock .NET MAUI (Microsoft.Maui.Essentials, already referenced by this app — no new
            // package needed) does not include a native "Save As" dialog; that's
            // CommunityToolkit.Maui.Storage's IFileSaver, a separate package this app does not
            // currently reference (only CommunityToolkit.Maui core is installed). Share.RequestAsync
            // is the built-in equivalent: it hands the exported file to the platform share sheet so
            // the user can save or send it wherever they choose (Drive, Downloads via a file manager,
            // email, etc.).
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = AppResources.Settings_ExportButton,
                File = new ShareFile(exportPath)
            });

            StatusMessage = AppResources.Settings_ExportSuccess;
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Settings_ExportError;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.Settings_RestoreButton
            });
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Settings_RestoreError;
            return;
        }

        if (file is null)
            return;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.Settings_RestoreConfirmTitle,
            AppResources.Settings_RestoreConfirmMessage,
            AppResources.Settings_RestoreConfirmAccept,
            AppResources.Settings_RestoreConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await using var stream = await file.OpenReadAsync();
            await _backupService.RestoreAsync(stream);

            StatusMessage = AppResources.Settings_RestoreSuccess;
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Settings_RestoreError;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
