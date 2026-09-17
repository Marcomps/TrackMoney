using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CardNetworks;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddCardNetworkViewModel : ObservableObject
{
    private readonly ICardNetworkRepository _networkRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public AddCardNetworkViewModel(ICardNetworkRepository networkRepository)
    {
        _networkRepository = networkRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddCardNetwork_ValidationNameRequired;
            return;
        }

        var network = new CardNetwork(Name);

        IsBusy = true;
        try
        {
            await _networkRepository.AddAsync(network);
            await _networkRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
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
