using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddCreditCardViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string issuer = string.Empty;

    [ObservableProperty]
    private string? lastFourDigits;

    [ObservableProperty]
    private CurrencyCode selectedCurrency = CurrencyCode.USD;

    [ObservableProperty]
    private string creditLimitText = string.Empty;

    [ObservableProperty]
    private string openingAmountOwedText = "0";

    [ObservableProperty]
    private string? annualInterestRateText;

    [ObservableProperty]
    private string? monthlyInterestRateText;

    [ObservableProperty]
    private string statementCutOffDayText = string.Empty;

    [ObservableProperty]
    private string paymentDueDayText = string.Empty;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public AddCreditCardViewModel(ICreditAccountRepository creditAccountRepository)
    {
        _creditAccountRepository = creditAccountRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationNameRequired;
            return;
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationIssuerRequired;
            return;
        }

        if (!string.IsNullOrWhiteSpace(LastFourDigits) &&
            (LastFourDigits.Length != 4 || !LastFourDigits.All(char.IsDigit)))
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationLastFourDigitsInvalid;
            return;
        }

        if (!decimal.TryParse(CreditLimitText, NumberStyles.Number, CultureInfo.CurrentCulture, out var creditLimit) || creditLimit <= 0)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationCreditLimitInvalid;
            return;
        }

        if (!decimal.TryParse(OpeningAmountOwedText, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingAmountOwed) || openingAmountOwed < 0)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationAmountOwedInvalid;
            return;
        }

        decimal? annualInterestRate = null;
        if (!string.IsNullOrWhiteSpace(AnnualInterestRateText))
        {
            if (!decimal.TryParse(AnnualInterestRateText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedAnnualRate) || parsedAnnualRate < 0)
            {
                ErrorMessage = AppResources.AddCreditCard_ValidationAnnualInterestRateInvalid;
                return;
            }

            annualInterestRate = parsedAnnualRate;
        }

        decimal? monthlyInterestRate = null;
        if (!string.IsNullOrWhiteSpace(MonthlyInterestRateText))
        {
            if (!decimal.TryParse(MonthlyInterestRateText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedMonthlyRate) || parsedMonthlyRate < 0)
            {
                ErrorMessage = AppResources.AddCreditCard_ValidationMonthlyInterestRateInvalid;
                return;
            }

            monthlyInterestRate = parsedMonthlyRate;
        }

        if (!int.TryParse(StatementCutOffDayText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var statementCutOffDay) || statementCutOffDay is < 1 or > 31)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationStatementCutOffDayInvalid;
            return;
        }

        if (!int.TryParse(PaymentDueDayText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var paymentDueDay) || paymentDueDay is < 1 or > 31)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationPaymentDueDayInvalid;
            return;
        }

        var creditCard = new CreditCard(
            Name,
            SelectedCurrency,
            Issuer,
            creditLimit,
            statementCutOffDay,
            paymentDueDay,
            openingAmountOwed,
            string.IsNullOrWhiteSpace(LastFourDigits) ? null : LastFourDigits,
            annualInterestRate,
            monthlyInterestRate,
            Notes);

        IsBusy = true;
        try
        {
            await _creditAccountRepository.AddAsync(creditCard);
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
