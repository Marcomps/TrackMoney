using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CardNetworksListViewModel : ObservableObject
{
    private readonly ICardNetworkRepository _networkRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<CardNetworkListItem> Networks { get; } = [];

    public bool IsEmpty => HasLoaded && Networks.Count == 0;

    public CardNetworksListViewModel(ICardNetworkRepository networkRepository)
    {
        _networkRepository = networkRepository;
    }

    [RelayCommand]
    private async Task LoadNetworksAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var networks = await _networkRepository.GetAllAsync();

            Networks.Clear();
            foreach (var network in networks.OrderBy(n => n.Name))
                Networks.Add(CardNetworkListItem.FromDomain(network));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddCardNetworkAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddCardNetworkPage));
    }
}
