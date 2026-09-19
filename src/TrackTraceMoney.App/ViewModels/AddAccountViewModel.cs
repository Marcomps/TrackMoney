using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Accounts;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Institution (the bank behind a <see cref="AccountKind.Bank"/> account) is picked from the user's own
/// growing <see cref="IFinancialInstitutionRepository"/> list, mirroring AddCreditCardViewModel/
/// AddLoanViewModel's Institution picker — replaces the free-text BankName entry this screen used to
/// show (see BankAccount.BankName's own remarks). Unlike those two screens, the institution here is
/// optional (a "None" sentinel option is included) because BankAccount's old free-text field was always
/// optional too — see BankAccount.InstitutionId's remarks for why this one deliberately doesn't mirror
/// CreditCard/Loan/TermDeposit/InvestmentFund's required-ness.
///
/// Also doubles as the edit screen (edit/delete slice spec §1) when navigated to with an
/// <c>accountId</c> query parameter: <see cref="LoadOptionsAsync"/> additionally prefills every field
/// from the existing account and <see cref="SaveAsync"/> calls the account's mutators instead of
/// constructing a new one. <c>Balance</c> is never editable here (no such field exists on this
/// screen even in edit mode) — a wrong starting balance is corrected via an adjusting transaction, per
/// the spec's own explicit decision.
/// </summary>
[QueryProperty(nameof(AccountIdText), "accountId")]
public sealed partial class AddAccountViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;
    private readonly IFinancialAccountLifecycleService _accountLifecycleService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsOpeningBalanceVisible))]
    [NotifyPropertyChangedFor(nameof(IsKindPickerEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyPickerEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyLockedMessageVisible))]
    private Guid? editingAccountId;

    /// <summary>
    /// The actual <c>[QueryProperty]</c> target -- a non-nullable <see cref="Guid"/> can't be bound
    /// directly (MAUI Shell's internal <c>Convert.ChangeType</c> throws <see cref="InvalidCastException"/>
    /// for it; see <c>EditRecurringIncomeAmountViewModel.RecurringIncomeIdText</c>'s doc comment, the
    /// precedent this copies). Absent/unparseable simply means "add mode", not an error — unlike that
    /// precedent, this query parameter is genuinely optional (a bare navigation to this page with no
    /// <c>accountId</c> is the normal Add flow).
    /// </summary>
    [ObservableProperty]
    private string? accountIdText;

    partial void OnAccountIdTextChanged(string? value) =>
        EditingAccountId = Guid.TryParse(value, out var parsed) ? parsed : null;

    public bool IsEditMode => EditingAccountId is not null;

    public string PageTitle => IsEditMode ? AppResources.AddAccount_EditTitle : AppResources.AddAccount_Title;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBankKind))]
    [NotifyPropertyChangedFor(nameof(IsSavingsKind))]
    private AccountKind selectedKind = AccountKind.Cash;

    [ObservableProperty]
    private CurrencyCode selectedCurrency = CurrencyCode.USD;

    [ObservableProperty]
    private string openingBalanceText = "0";

    [ObservableProperty]
    private NamedOption? selectedInstitution;

    [ObservableProperty]
    private string? accountNumberLast4;

    [ObservableProperty]
    private string? goalAmountText;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// True whenever a currency edit is safe (add mode is always "true" here since the picker's
    /// enabled-ness is gated by <see cref="IsCurrencyPickerEnabled"/>, not this flag alone) — set from
    /// <c>IFinancialAccountLifecycleService.CanChangeCurrencyAsync</c> once in edit mode (edit/delete
    /// slice spec §1.1): mutable only when zero transactions reference the account.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrencyPickerEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyLockedMessageVisible))]
    private bool canChangeCurrency = true;

    public bool IsOpeningBalanceVisible => !IsEditMode;

    public bool IsKindPickerEnabled => !IsEditMode;

    public bool IsCurrencyPickerEnabled => !IsEditMode || CanChangeCurrency;

    /// <summary>
    /// Shown instead of silently disabling the Currency picker (edit/delete slice spec §1.1/§2's "show
    /// why it's disabled, don't just silently disable it" requirement, applied here to Account edit too).
    /// </summary>
    public bool IsCurrencyLockedMessageVisible => IsEditMode && !CanChangeCurrency;

    public IReadOnlyList<AccountKind> AvailableKinds { get; } = Enum.GetValues<AccountKind>();

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public bool IsBankKind => SelectedKind == AccountKind.Bank;

    public bool IsSavingsKind => SelectedKind == AccountKind.Savings;

    public ObservableCollection<NamedOption> InstitutionOptions { get; } = [];

    public AddAccountViewModel(
        IFinancialAccountRepository accountRepository,
        IFinancialInstitutionRepository institutionRepository,
        IFinancialAccountLifecycleService accountLifecycleService)
    {
        _accountRepository = accountRepository;
        _institutionRepository = institutionRepository;
        _accountLifecycleService = accountLifecycleService;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var institutions = await _institutionRepository.GetAllAsync();

        InstitutionOptions.Clear();
        InstitutionOptions.Add(new NamedOption(Guid.Empty, AppResources.AddTransaction_NoneOption));
        foreach (var institution in institutions.OrderBy(i => i.Name))
            InstitutionOptions.Add(new NamedOption(institution.Id, institution.Name));

        if (EditingAccountId is not { } accountId)
            return;

        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            ErrorMessage = AppResources.AddAccount_EditNotFound;
            return;
        }

        Name = account.Name;
        SelectedKind = account switch
        {
            CashAccount => AccountKind.Cash,
            BankAccount => AccountKind.Bank,
            SavingsAccount => AccountKind.Savings,
            _ => SelectedKind
        };
        SelectedCurrency = account.Currency;
        Notes = account.Notes;

        CanChangeCurrency = await _accountLifecycleService.CanChangeCurrencyAsync(accountId);

        switch (account)
        {
            case BankAccount bankAccount:
                SelectedInstitution = bankAccount.InstitutionId is { } institutionId
                    ? InstitutionOptions.FirstOrDefault(o => o.Id == institutionId)
                    : InstitutionOptions[0];
                AccountNumberLast4 = bankAccount.AccountNumberLast4;
                break;
            case SavingsAccount savingsAccount:
                GoalAmountText = savingsAccount.GoalAmount?.ToString("N2", CultureInfo.CurrentCulture);
                break;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddAccount_ValidationNameRequired;
            return;
        }

        decimal? goalAmount = null;
        if (SelectedKind == AccountKind.Savings && !string.IsNullOrWhiteSpace(GoalAmountText))
        {
            if (!decimal.TryParse(GoalAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedGoal) || parsedGoal < 0)
            {
                ErrorMessage = AppResources.AddAccount_ValidationGoalInvalid;
                return;
            }

            goalAmount = parsedGoal;
        }

        if (IsEditMode)
        {
            await SaveEditAsync(goalAmount);
            return;
        }

        if (!decimal.TryParse(OpeningBalanceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingBalance))
        {
            ErrorMessage = AppResources.AddAccount_ValidationBalanceInvalid;
            return;
        }

        FinancialAccount account = SelectedKind switch
        {
            AccountKind.Cash => new CashAccount(Name, SelectedCurrency, openingBalance, Notes),
            AccountKind.Bank => new BankAccount(Name, SelectedCurrency, openingBalance, AsNullableId(SelectedInstitution), AccountNumberLast4, Notes),
            AccountKind.Savings => new SavingsAccount(Name, SelectedCurrency, openingBalance, goalAmount, Notes),
            _ => throw new NotSupportedException($"Unknown account kind '{SelectedKind}'.")
        };

        IsBusy = true;
        try
        {
            await _accountRepository.AddAsync(account);
            await _accountRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Edit-mode half of <see cref="SaveAsync"/> (edit/delete slice spec §1.1) -- mutates the existing
    /// account in place instead of constructing a new one. <see cref="FinancialAccount.Balance"/> is
    /// never touched here (no mutator exists for it, deliberately).
    /// </summary>
    private async Task SaveEditAsync(decimal? goalAmount)
    {
        IsBusy = true;
        try
        {
            var account = await _accountRepository.GetByIdAsync(EditingAccountId!.Value);
            if (account is null)
            {
                ErrorMessage = AppResources.AddAccount_EditNotFound;
                return;
            }

            account.Rename(Name);
            account.UpdateNotes(Notes);

            if (CanChangeCurrency)
                account.UpdateCurrency(SelectedCurrency);

            switch (account)
            {
                case BankAccount bankAccount:
                    bankAccount.UpdateInstitution(AsNullableId(SelectedInstitution));
                    bankAccount.UpdateAccountNumber(AccountNumberLast4);
                    break;
                case SavingsAccount savingsAccount:
                    savingsAccount.SetGoal(goalAmount);
                    break;
            }

            await _accountRepository.SaveChangesAsync();
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

    /// <summary>Same "None" sentinel convention as <c>AddCreditCardViewModel.AsNullableId</c>.</summary>
    private static Guid? AsNullableId(NamedOption? option) =>
        option is null || option.Id == Guid.Empty ? null : option.Id;
}
