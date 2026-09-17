using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Institution (README §19) is picked from the user's own growing
/// <see cref="IFinancialInstitutionRepository"/> list — see the
/// financial-institution-card-network-slice-spec's Decision 4. Required (mirrors the old free-text
/// Institution field's required-ness). No inline-add — a user without their bank listed yet leaves this
/// screen, adds it via Settings, and comes back, same as Category/Person elsewhere in this app.
/// </summary>
public sealed partial class AddLoanViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedInstitution;

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

    public ObservableCollection<NamedOption> InstitutionOptions { get; } = [];

    public AddLoanViewModel(ICreditAccountRepository creditAccountRepository, IFinancialInstitutionRepository institutionRepository)
    {
        _creditAccountRepository = creditAccountRepository;
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
            ErrorMessage = AppResources.AddLoan_ValidationNameRequired;
            return;
        }

        if (SelectedInstitution is null)
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
            SelectedInstitution.Id,
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
