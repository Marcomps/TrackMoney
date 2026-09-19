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
///
/// Also doubles as the edit screen (edit/delete slice spec §3) when navigated to with a
/// <c>loanId</c> query parameter — same shape as <c>AddAccountViewModel</c>/<c>AddCreditCardViewModel</c>'s
/// edit mode. <see cref="Loan.OriginalAmount"/> and <see cref="CreditAccount.AmountOwed"/> are never
/// editable (both hidden in edit mode): <c>OriginalAmount</c> is documented as fixed for the life of the
/// loan, and <c>AmountOwed</c> has no direct setter anywhere except <c>RegisterPayment</c>.
/// <see cref="NextPaymentDate"/>/<see cref="RequiredPaymentText"/> ARE editable in edit mode, via the
/// existing <see cref="Loan.AdvanceSchedule"/> — no new mutator needed, per the spec's exact wording.
///
/// Currency editing is NOT offered in edit mode -- same "known gap" as <c>AddCreditCardViewModel</c>'s
/// doc comment: no <c>UpdateCurrency</c> mutator exists on <see cref="CreditAccount"/>/<see cref="Loan"/>.
/// </summary>
[QueryProperty(nameof(LoanIdText), "loanId")]
public sealed partial class AddLoanViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsOriginalAmountVisible))]
    [NotifyPropertyChangedFor(nameof(IsCurrentBalanceVisible))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyPickerEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyLockedMessageVisible))]
    private Guid? editingLoanId;

    /// <summary>Same defensive-parse idiom as <c>AddAccountViewModel.AccountIdText</c>.</summary>
    [ObservableProperty]
    private string? loanIdText;

    partial void OnLoanIdTextChanged(string? value) =>
        EditingLoanId = Guid.TryParse(value, out var parsed) ? parsed : null;

    public bool IsEditMode => EditingLoanId is not null;

    public string PageTitle => IsEditMode ? AppResources.AddLoan_EditTitle : AppResources.AddLoan_Title;

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

    public bool IsOriginalAmountVisible => !IsEditMode;

    public bool IsCurrentBalanceVisible => !IsEditMode;

    /// <summary>Always disabled in edit mode -- see this class's doc comment's "known gap" note.</summary>
    public bool IsCurrencyPickerEnabled => !IsEditMode;

    public bool IsCurrencyLockedMessageVisible => IsEditMode;

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

        if (EditingLoanId is not { } loanId)
            return;

        var creditAccount = await _creditAccountRepository.GetByIdAsync(loanId);
        if (creditAccount is not Loan loan)
        {
            ErrorMessage = AppResources.AddLoan_EditNotFound;
            return;
        }

        Name = loan.Name;
        SelectedCurrency = loan.Currency;
        SelectedInstitution = loan.InstitutionId is { } institutionId
            ? InstitutionOptions.FirstOrDefault(o => o.Id == institutionId)
            : null;
        SelectedKind = loan.Kind;
        InterestRateText = loan.InterestRate.ToString("N2", CultureInfo.CurrentCulture);
        SelectedRateType = loan.RateType;
        MonthlyInstallmentText = loan.MonthlyInstallment.ToString("N2", CultureInfo.CurrentCulture);
        NextPaymentDate = loan.NextPaymentDate.ToDateTime(TimeOnly.MinValue);
        RequiredPaymentText = loan.RequiredPayment.ToString("N2", CultureInfo.CurrentCulture);
        FeesText = loan.Fees?.ToString("N2", CultureInfo.CurrentCulture);
        Notes = loan.Notes;
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

        if (IsEditMode)
        {
            await SaveEditAsync(interestRate, monthlyInstallment, requiredPayment, fees);
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

    /// <summary>
    /// Edit-mode half of <see cref="SaveAsync"/> (edit/delete slice spec §3.1) -- calls
    /// <see cref="Loan.UpdateDetails"/> for the informational fields and the EXISTING, UNCHANGED
    /// <see cref="Loan.AdvanceSchedule"/> for <see cref="NextPaymentDate"/>/<paramref name="requiredPayment"/>,
    /// per the spec's exact instruction not to add a new mutator for those two fields.
    /// </summary>
    private async Task SaveEditAsync(decimal interestRate, decimal monthlyInstallment, decimal requiredPayment, decimal? fees)
    {
        IsBusy = true;
        try
        {
            var creditAccount = await _creditAccountRepository.GetByIdAsync(EditingLoanId!.Value);
            if (creditAccount is not Loan loan)
            {
                ErrorMessage = AppResources.AddLoan_EditNotFound;
                return;
            }

            loan.Rename(Name);
            loan.UpdateNotes(Notes);

            loan.UpdateDetails(
                SelectedInstitution!.Id,
                SelectedKind,
                interestRate,
                SelectedRateType,
                monthlyInstallment,
                fees);

            loan.AdvanceSchedule(DateOnly.FromDateTime(NextPaymentDate), requiredPayment);

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
