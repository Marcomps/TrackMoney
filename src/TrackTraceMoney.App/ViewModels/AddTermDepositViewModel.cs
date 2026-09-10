using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddTermDepositViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string institution = string.Empty;

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

    public AddTermDepositViewModel(IFinancialAccountRepository financialAccountRepository)
    {
        _financialAccountRepository = financialAccountRepository;
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

        if (string.IsNullOrWhiteSpace(Institution))
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
            Institution,
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
