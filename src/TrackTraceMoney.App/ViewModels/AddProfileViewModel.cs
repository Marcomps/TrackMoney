using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddProfileViewModel : ObservableObject
{
    private readonly IProfileManagementService _profileManagementService;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public AddProfileViewModel(IProfileManagementService profileManagementService)
    {
        _profileManagementService = profileManagementService;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddProfile_ValidationNameRequired;
            return;
        }

        IsBusy = true;
        try
        {
            // Only adds a profile to the catalog -- does not activate it and does not require a
            // restart, unlike ProfilesListViewModel.SwitchProfileAsync/CreateFirstProfileViewModel.
            await _profileManagementService.CreateProfileAsync(Name);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.AddProfile_Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
