using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class FinancialInstitutionsListViewModel : ObservableObject
{
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<FinancialInstitutionListItem> Institutions { get; } = [];

    public bool IsEmpty => HasLoaded && Institutions.Count == 0;

    public FinancialInstitutionsListViewModel(IFinancialInstitutionRepository institutionRepository)
    {
        _institutionRepository = institutionRepository;
    }

    [RelayCommand]
    private async Task LoadInstitutionsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var institutions = await _institutionRepository.GetAllAsync();

            Institutions.Clear();
            foreach (var institution in institutions.OrderBy(i => i.Name))
                Institutions.Add(FinancialInstitutionListItem.FromDomain(institution));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddFinancialInstitutionAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddFinancialInstitutionPage));
    }
}
