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

    private bool _isLoading;

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
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
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
            _isLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddCardNetworkAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddCardNetworkPage));
    }
}
