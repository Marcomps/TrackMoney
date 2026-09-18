using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Backs <see cref="Views.SetPinPage"/> — README §43 app-lock Story 1's "Set PIN" flow, reached from
/// Settings when the user turns the "App Lock" toggle on. Shows the no-in-app-reset disclosure
/// (app-lock-slice-spec Decision 5) before the PIN can be saved.
/// </summary>
public sealed partial class SetPinViewModel : ObservableObject
{
    private readonly IAppLockService _appLockService;

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private string confirmPin = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public SetPinViewModel(IAppLockService appLockService)
    {
        _appLockService = appLockService;
    }

    private static bool IsSixDigits(string value) => value.Length == 6 && value.All(char.IsDigit);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!IsSixDigits(Pin) || !IsSixDigits(ConfirmPin))
        {
            ErrorMessage = AppResources.SetPin_ValidationPinInvalid;
            return;
        }

        if (Pin != ConfirmPin)
        {
            ErrorMessage = AppResources.SetPin_ValidationPinMismatch;
            return;
        }

        IsBusy = true;
        try
        {
            await _appLockService.EnablePinAsync(Pin);
        }
        finally
        {
            // Never let the raw PIN linger in the bound Entry's backing field longer than this method
            // needs it, regardless of outcome.
            Pin = string.Empty;
            ConfirmPin = string.Empty;
            IsBusy = false;
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
