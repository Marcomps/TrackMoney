using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalBackupService _backupService;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isBusy;

    public SettingsViewModel(ILocalBackupService backupService)
    {
        _backupService = backupService;
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
