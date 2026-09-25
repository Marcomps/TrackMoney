using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.TransactionPresets;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// List+Add+Edit shell for <see cref="Domain.TransactionPresets.TransactionPreset"/> (Transaction Type
/// Customization slice spec §B.4/§B.7.4), mirroring <see cref="CategoriesListViewModel"/>'s exact shape
/// (Show-inactive toggle, SwipeView Edit/Deactivate/Reactivate/Delete).
/// </summary>
public sealed partial class TransactionPresetsListViewModel : ObservableObject
{
    private readonly ITransactionPresetRepository _transactionPresetRepository;
    private readonly ITransactionPresetLifecycleService _transactionPresetLifecycleService;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    /// <summary>Mirrors <c>CategoriesListViewModel.ShowInactive</c>'s exact shape -- without this toggle,
    /// Deactivate is a one-way trap.</summary>
    [ObservableProperty]
    private bool showInactive;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<TransactionPresetListItem> Presets { get; } = [];

    public bool IsEmpty => HasLoaded && Presets.Count == 0;

    public TransactionPresetsListViewModel(
        ITransactionPresetRepository transactionPresetRepository,
        ITransactionPresetLifecycleService transactionPresetLifecycleService)
    {
        _transactionPresetRepository = transactionPresetRepository;
        _transactionPresetLifecycleService = transactionPresetLifecycleService;
    }

    partial void OnShowInactiveChanged(bool value) => LoadPresetsCommand.Execute(null);

    [RelayCommand]
    private async Task LoadPresetsAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var presets = ShowInactive
                ? await _transactionPresetRepository.GetAllAsync()
                : await _transactionPresetRepository.GetActiveAsync();

            Presets.Clear();
            foreach (var preset in presets.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name))
                Presets.Add(TransactionPresetListItem.FromDomain(preset));

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
    private static async Task AddPresetAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddTransactionPresetPage));
    }

    [RelayCommand]
    private static async Task EditPresetAsync(Guid presetId) =>
        await Shell.Current.GoToAsync($"{nameof(EditTransactionPresetPage)}?presetId={presetId}");

    [RelayCommand]
    private async Task DeletePresetAsync(Guid presetId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.TransactionPresetsList_DeleteConfirmTitle,
            AppResources.TransactionPresetsList_DeleteConfirmMessage,
            AppResources.TransactionPresetsList_DeleteConfirmAccept,
            AppResources.TransactionPresetsList_DeleteConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _transactionPresetLifecycleService.DeleteAsync(presetId);
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = AppResources.TransactionPresetsList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadPresetsAsync();
    }

    [RelayCommand]
    private async Task DeactivatePresetAsync(Guid presetId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.TransactionPresetsList_DeactivateConfirmTitle,
            AppResources.TransactionPresetsList_DeactivateConfirmMessage,
            AppResources.TransactionPresetsList_DeactivateConfirmAccept,
            AppResources.TransactionPresetsList_DeactivateConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _transactionPresetLifecycleService.DeactivateAsync(presetId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.TransactionPresetsList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadPresetsAsync();
    }

    [RelayCommand]
    private async Task ReactivatePresetAsync(Guid presetId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _transactionPresetLifecycleService.ReactivateAsync(presetId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.TransactionPresetsList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadPresetsAsync();
    }
}
