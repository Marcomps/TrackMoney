using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddLoanViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string institution = string.Empty;

    [ObservableProperty]
    private LoanKind selectedKind = LoanKind.PersonalLoan;

    [ObservableProperty]
    private CurrencyCode selectedCurrency = CurrencyCode.USD;

    [ObservableProperty]
    private string originalAmountText = string.Empty;

    [ObservableProperty]
    private string currentBalanceText = string.Empty;

    [ObservableProperty]
    private string interestRateText = string.Empty;

    [ObservableProperty]
    private LoanRateType selectedRateType = LoanRateType.Fixed;

    [ObservableProperty]
    private string monthlyInstallmentText = string.Empty;

    [ObservableProperty]
    private DateTime nextPaymentDate = DateTime.Today;

    [ObservableProperty]
    private string requiredPaymentText = string.Empty;

    [ObservableProperty]
    private string? feesText;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<LoanKind> AvailableKinds { get; } = Enum.GetValues<LoanKind>();

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public IReadOnlyList<LoanRateType> AvailableRateTypes { get; } = Enum.GetValues<LoanRateType>();

    public AddLoanViewModel(ICreditAccountRepository creditAccountRepository)
    {
        _creditAccountRepository = creditAccountRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddLoan_ValidationNameRequired;
            return;
        }

        if (string.IsNullOrWhiteSpace(Institution))
        {
            ErrorMessage = AppResources.AddLoan_ValidationInstitutionRequired;
            return;
        }

        if (!decimal.TryParse(OriginalAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var originalAmount) || originalAmount <= 0)
        {
            ErrorMessage = AppResources.AddLoan_ValidationOriginalAmountInvalid;
            return;
        }

        if (!decimal.TryParse(CurrentBalanceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentBalance) || currentBalance < 0)
        {
            ErrorMessage = AppResources.AddLoan_ValidationCurrentBalanceInvalid;
            return;
        }

        if (!decimal.TryParse(InterestRateText, NumberStyles.Number, CultureInfo.CurrentCulture, out var interestRate) || interestRate < 0)
        {
            ErrorMessage = AppResources.AddLoan_ValidationInterestRateInvalid;
            return;
        }

        if (!decimal.TryParse(MonthlyInstallmentText, NumberStyles.Number, CultureInfo.CurrentCulture, out var monthlyInstallment) || monthlyInstallment <= 0)
        {
            ErrorMessage = AppResources.AddLoan_ValidationMonthlyInstallmentInvalid;
            return;
        }

        if (!decimal.TryParse(RequiredPaymentText, NumberStyles.Number, CultureInfo.CurrentCulture, out var requiredPayment) || requiredPayment <= 0)
        {
            ErrorMessage = AppResources.AddLoan_ValidationRequiredPaymentInvalid;
            return;
        }

        decimal? fees = null;
        if (!string.IsNullOrWhiteSpace(FeesText))
        {
            if (!decimal.TryParse(FeesText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedFees) || parsedFees < 0)
            {
                ErrorMessage = AppResources.AddLoan_ValidationFeesInvalid;
                return;
            }

            fees = parsedFees;
        }

        var loan = new Loan(
            Name,
            SelectedCurrency,
            Institution,
            SelectedKind,
            originalAmount,
            currentBalance,
            interestRate,
            SelectedRateType,
            monthlyInstallment,
            DateOnly.FromDateTime(NextPaymentDate),
            requiredPayment,
            fees,
            Notes);

        IsBusy = true;
        try
        {
            await _creditAccountRepository.AddAsync(loan);
            await _creditAccountRepository.SaveChangesAsync();
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
