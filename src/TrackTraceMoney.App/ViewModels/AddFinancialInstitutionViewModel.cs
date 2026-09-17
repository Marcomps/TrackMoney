using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Institutions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddFinancialInstitutionViewModel : ObservableObject
{
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public AddFinancialInstitutionViewModel(IFinancialInstitutionRepository institutionRepository)
    {
        _institutionRepository = institutionRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddFinancialInstitution_ValidationNameRequired;
            return;
        }

        var institution = new FinancialInstitution(Name);

        IsBusy = true;
        try
        {
            await _institutionRepository.AddAsync(institution);
            await _institutionRepository.SaveChangesAsync();
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
