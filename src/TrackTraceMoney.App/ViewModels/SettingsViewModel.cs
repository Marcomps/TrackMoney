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
        ICloudBackupService cloudBackupService)
    {
        _backupService = backupService;
        _cloudAuthService = cloudAuthService;
        _cloudBackupService = cloudBackupService;
    }

    public bool IsCloudUnauthenticated => !IsCloudAuthenticated;

    public string CloudAccountStatusText => string.Format(AppResources.CloudAccount_LoggedInAs, CloudAccountEmail);

    public string LastCloudBackupText => LastCloudBackupAtUtc.HasValue
        ? string.Format(AppResources.CloudBackup_LastBackupKnown, LastCloudBackupAtUtc)
        : AppResources.CloudBackup_LastBackupNone;

    partial void OnIsCloudAuthenticatedChanged(bool value) => OnPropertyChanged(nameof(IsCloudUnauthenticated));

    partial void OnCloudAccountEmailChanged(string? value) => OnPropertyChanged(nameof(CloudAccountStatusText));

    partial void OnLastCloudBackupAtUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastCloudBackupText));

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
        var result = await _cloudBackupService.GetStatusAsync();
        if (result.Success)
        {
            CloudBackupExists = result.Exists;
            LastCloudBackupAtUtc = result.LastBackupAtUtc;
        }
        else if (result.Error != CloudBackupResultError.NotAuthenticated)
        {
            // A logged-out status check failing isn't noteworthy; anything else is.
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
                }
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
            }
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
