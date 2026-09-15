using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Services.Cloud;
using TrackTraceMoney.App.Views;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CloudLoginViewModel : ObservableObject
{
    private readonly ICloudAuthService _cloudAuthService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public CloudLoginViewModel(ICloudAuthService cloudAuthService)
    {
        _cloudAuthService = cloudAuthService;
    }

    [RelayCommand]
    private async Task LoginAsync(CancellationToken ct)
    {
        ErrorMessage = null;

        var trimmedEmail = Email.Trim();
        if (string.IsNullOrEmpty(trimmedEmail))
        {
            ErrorMessage = AppResources.CloudAuth_ValidationEmailRequired;
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = AppResources.CloudAuth_ValidationPasswordRequired;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _cloudAuthService.LoginAsync(trimmedEmail, Password, ct);
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
    private static async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(CloudRegisterPage));
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string MapError(CloudAuthResultError error) => error switch
    {
        CloudAuthResultError.InvalidCredentials => AppResources.CloudLogin_ErrorInvalidCredentials,
        CloudAuthResultError.ValidationFailed => AppResources.CloudAuth_ErrorValidationFailed,
        CloudAuthResultError.NetworkUnavailable => AppResources.CloudAuth_ErrorNetworkUnavailable,
        _ => AppResources.CloudAuth_ErrorUnknown
    };
}
