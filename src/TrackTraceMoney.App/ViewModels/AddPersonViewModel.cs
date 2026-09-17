using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.People;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddPersonViewModel : ObservableObject
{
    private readonly IPersonRepository _personRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomRelationship))]
    private PersonRelationshipType selectedRelationshipType = PersonRelationshipType.Other;

    [ObservableProperty]
    private string customRelationshipLabel = string.Empty;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<PersonRelationshipType> AvailableRelationshipTypes { get; } = Enum.GetValues<PersonRelationshipType>();

    /// <summary>
    /// Shows/hides the free-text relationship field — same show/hide idiom as
    /// <c>AddTransactionViewModel</c>'s medical-expense insurance fields (bound to a computed property
    /// kept in sync via <c>[NotifyPropertyChangedFor]</c> on the triggering selection).
    /// </summary>
    public bool IsCustomRelationship => SelectedRelationshipType == PersonRelationshipType.Custom;

    public AddPersonViewModel(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddPerson_ValidationNameRequired;
            return;
        }

        if (IsCustomRelationship && string.IsNullOrWhiteSpace(CustomRelationshipLabel))
        {
            ErrorMessage = AppResources.AddPerson_ValidationCustomRelationshipLabelRequired;
            return;
        }

        var person = new Person(
            Name,
            SelectedRelationshipType,
            IsCustomRelationship ? CustomRelationshipLabel : null,
            Notes);

        IsBusy = true;
        try
        {
            await _personRepository.AddAsync(person);
            await _personRepository.SaveChangesAsync();
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
