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
        if (IsBusy)
            return;

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
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddPersonAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddPersonPage));
    }
}
