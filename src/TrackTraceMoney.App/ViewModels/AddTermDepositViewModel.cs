using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Institution (README §22) is picked from the user's own growing
/// <see cref="IFinancialInstitutionRepository"/> list — see the
/// financial-institution-card-network-slice-spec's Decision 4. Required (mirrors the old free-text
/// Institution field's required-ness). No inline-add — a user without their bank listed yet leaves this
/// screen, adds it via Settings, and comes back, same as Category/Person elsewhere in this app.
/// </summary>
public sealed partial class AddTermDepositViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedInstitution;

    [ObservableProperty]
    private CurrencyCode selectedCurrency = CurrencyCode.USD;

    [ObservableProperty]
    private string initialPrincipalText = string.Empty;

    [ObservableProperty]
    private string openingBalanceText = "0";

    [ObservableProperty]
    private string rateText = string.Empty;

    [ObservableProperty]
    private TermDepositRateType selectedRateType = TermDepositRateType.Nominal;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime maturityDate = DateTime.Today.AddYears(1);

    [ObservableProperty]
    private TermDepositInterestFrequency selectedInterestFrequency = TermDepositInterestFrequency.Monthly;

    [ObservableProperty]
    private bool isCompounding;

    [ObservableProperty]
    private bool autoRenewal;

    [ObservableProperty]
    private string? estimatedInterestText;

    [ObservableProperty]
    private string interestReceivedText = "0";

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public IReadOnlyList<TermDepositRateType> AvailableRateTypes { get; } = Enum.GetValues<TermDepositRateType>();

    public IReadOnlyList<TermDepositInterestFrequency> AvailableInterestFrequencies { get; } = Enum.GetValues<TermDepositInterestFrequency>();

    public ObservableCollection<NamedOption> InstitutionOptions { get; } = [];

    public AddTermDepositViewModel(IFinancialAccountRepository financialAccountRepository, IFinancialInstitutionRepository institutionRepository)
    {
        _financialAccountRepository = financialAccountRepository;
        _institutionRepository = institutionRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var institutions = await _institutionRepository.GetAllAsync();

        InstitutionOptions.Clear();
        foreach (var institution in institutions.OrderBy(i => i.Name))
            InstitutionOptions.Add(new NamedOption(institution.Id, institution.Name));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationNameRequired;
            return;
        }

        if (SelectedInstitution is null)
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationInstitutionRequired;
            return;
        }

        if (!decimal.TryParse(InitialPrincipalText, NumberStyles.Number, CultureInfo.CurrentCulture, out var initialPrincipal) || initialPrincipal <= 0)
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationInitialPrincipalInvalid;
            return;
        }

        if (!decimal.TryParse(OpeningBalanceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingBalance) || openingBalance < 0)
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationOpeningBalanceInvalid;
            return;
        }

        if (!decimal.TryParse(RateText, NumberStyles.Number, CultureInfo.CurrentCulture, out var rate) || rate < 0)
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationRateInvalid;
            return;
        }

        if (MaturityDate <= StartDate)
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationMaturityDateInvalid;
            return;
        }

        decimal? estimatedInterest = null;
        if (!string.IsNullOrWhiteSpace(EstimatedInterestText))
        {
            if (!decimal.TryParse(EstimatedInterestText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedEstimatedInterest) || parsedEstimatedInterest < 0)
            {
                ErrorMessage = AppResources.AddTermDeposit_ValidationEstimatedInterestInvalid;
                return;
            }

            estimatedInterest = parsedEstimatedInterest;
        }

        if (!decimal.TryParse(InterestReceivedText, NumberStyles.Number, CultureInfo.CurrentCulture, out var interestReceived) || interestReceived < 0)
        {
            ErrorMessage = AppResources.AddTermDeposit_ValidationInterestReceivedInvalid;
            return;
        }

        var termDeposit = new TermDeposit(
            Name,
            SelectedCurrency,
            SelectedInstitution.Id,
            initialPrincipal,
            openingBalance,
            rate,
            SelectedRateType,
            DateOnly.FromDateTime(StartDate),
            DateOnly.FromDateTime(MaturityDate),
            SelectedInterestFrequency,
            IsCompounding,
            AutoRenewal,
            estimatedInterest,
            interestReceived,
            Notes);

        IsBusy = true;
        try
        {
            await _financialAccountRepository.AddAsync(termDeposit);
            await _financialAccountRepository.SaveChangesAsync();
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
