using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class ProfilesListViewModel : ObservableObject
{
    private readonly ILocalProfileRepository _profileRepository;
    private readonly IActiveProfileStore _activeProfileStore;
    private readonly IProfileManagementService _profileManagementService;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<ProfileListItem> Profiles { get; } = [];

    public bool IsEmpty => HasLoaded && Profiles.Count == 0;

    public ProfilesListViewModel(
        ILocalProfileRepository profileRepository,
        IActiveProfileStore activeProfileStore,
        IProfileManagementService profileManagementService)
    {
        _profileRepository = profileRepository;
        _activeProfileStore = activeProfileStore;
        _profileManagementService = profileManagementService;
    }

    [RelayCommand]
    private async Task LoadProfilesAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var profiles = await _profileRepository.GetAllAsync();
            var activeProfileId = await _activeProfileStore.GetActiveProfileIdAsync();

            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(ProfileListItem.FromDomain(profile, profile.Id == activeProfileId));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddProfileAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddProfilePage));
    }

    [RelayCommand]
    private async Task SwitchProfileAsync(ProfileListItem? profile)
    {
        if (profile is null || profile.IsActive)
            return;

        StatusMessage = null;
        ErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.ProfilesList_SwitchConfirmTitle,
            string.Format(AppResources.ProfilesList_SwitchConfirmMessage, profile.Name),
            AppResources.ProfilesList_SwitchConfirmAccept,
            AppResources.ProfilesList_SwitchConfirmCancel);

        if (!confirmed)
            return;

        try
        {
            await _activeProfileStore.SetActiveProfileIdAsync(profile.Id);
            StatusMessage = string.Format(AppResources.ProfilesList_SwitchedMessage, profile.Name);
            await LoadProfilesAsync();
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.ProfilesList_Error;
        }
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(ProfileListItem? profile)
    {
        if (profile is null)
            return;

        StatusMessage = null;
        ErrorMessage = null;

        // Both pieces of information (the usual delete warning, plus -- only when relevant -- the
        // extra active-profile notice) are folded into one confirm dialog's message rather than a
        // second dialog, per this feature's spec.
        var message = string.Format(AppResources.ProfilesList_DeleteConfirmMessage, profile.Name);
        if (profile.IsActive)
            message = $"{message}{Environment.NewLine}{Environment.NewLine}{AppResources.ProfilesList_DeleteActiveProfileNotice}";

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.ProfilesList_DeleteConfirmTitle,
            message,
            AppResources.ProfilesList_DeleteConfirmAccept,
            AppResources.ProfilesList_DeleteConfirmCancel);

        if (!confirmed)
            return;

        try
        {
            await _profileManagementService.DeleteProfileAsync(profile.Id);
            StatusMessage = AppResources.ProfilesList_DeletedMessage;
            await LoadProfilesAsync();
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.ProfilesList_Error;
        }
    }
}
