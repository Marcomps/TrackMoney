using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class PeopleListViewModel : ObservableObject
{
    private readonly IPersonRepository _personRepository;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<PersonListItem> People { get; } = [];

    public bool IsEmpty => HasLoaded && People.Count == 0;

    public PeopleListViewModel(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    [RelayCommand]
    private async Task LoadPeopleAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var people = await _personRepository.GetAllAsync();

            People.Clear();
            foreach (var person in people.OrderBy(p => p.Name))
                People.Add(PersonListItem.FromDomain(person, PersonRelationshipTypeToLabelConverter.GetDisplayLabel(person)));

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
    private static async Task AddPersonAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddPersonPage));
    }
}
