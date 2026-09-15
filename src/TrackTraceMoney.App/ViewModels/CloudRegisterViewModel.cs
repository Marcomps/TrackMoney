using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Services.Cloud;
using TrackTraceMoney.App.Views;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CloudRegisterViewModel : ObservableObject
{
    private static readonly Regex EmailShapeRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICloudAuthService _cloudAuthService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public CloudRegisterViewModel(ICloudAuthService cloudAuthService)
    {
        _cloudAuthService = cloudAuthService;
    }

    [RelayCommand]
    private async Task RegisterAsync(CancellationToken ct)
    {
        ErrorMessage = null;

        var trimmedEmail = Email.Trim();
        if (string.IsNullOrEmpty(trimmedEmail))
        {
            ErrorMessage = AppResources.CloudAuth_ValidationEmailRequired;
            return;
        }

        if (!EmailShapeRegex.IsMatch(trimmedEmail))
        {
            ErrorMessage = AppResources.CloudRegister_ValidationEmailInvalid;
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = AppResources.CloudAuth_ValidationPasswordRequired;
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = AppResources.CloudRegister_ValidationPasswordTooShort;
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = AppResources.CloudRegister_ValidationPasswordMismatch;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _cloudAuthService.RegisterAsync(trimmedEmail, Password, ct);
            if (result.Success)
            {
                await Shell.Current.GoToAsync("//Settings");
            }
            else
            {
                ErrorMessage = MapError(result.Error!.Value);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync(nameof(CloudLoginPage));
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string MapError(CloudAuthResultError error) => error switch
    {
        CloudAuthResultError.DuplicateEmail => AppResources.CloudRegister_ErrorDuplicateEmail,
        CloudAuthResultError.ValidationFailed => AppResources.CloudAuth_ErrorValidationFailed,
        CloudAuthResultError.NetworkUnavailable => AppResources.CloudAuth_ErrorNetworkUnavailable,
        _ => AppResources.CloudAuth_ErrorUnknown
    };
}
