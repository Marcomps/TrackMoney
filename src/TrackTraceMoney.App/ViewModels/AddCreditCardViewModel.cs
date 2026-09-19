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
/// Institution (README §14's "Bank / issuer") and network/brand are both picked from the user's own
/// growing <see cref="IFinancialInstitutionRepository"/>/<see cref="ICardNetworkRepository"/> lists —
/// see the financial-institution-card-network-slice-spec's Decision 4. Institution is required (mirrors
/// the old free-text Issuer field's required-ness); network is optional (Slice B, a brand-new enrichment
/// field). Neither picker supports inline-add — a user without their bank/network listed yet leaves this
/// screen, adds it via Settings, and comes back, same as Category/Person elsewhere in this app.
///
/// Also doubles as the edit screen (edit/delete slice spec §2) when navigated to with a
/// <c>creditAccountId</c> query parameter — same shape as <c>AddAccountViewModel</c>'s edit mode.
/// <see cref="CreditAccount.AmountOwed"/> is never editable (no such field exists in edit mode) --
/// it has no direct setter anywhere except <c>RegisterCharge</c>/<c>RegisterPayment</c>.
///
/// <para><b>Known gap, flagged rather than silently worked around</b>: unlike
/// <see cref="Domain.Accounts.FinancialAccount.UpdateCurrency"/> (which exists), no equivalent
/// <c>UpdateCurrency</c> mutator exists anywhere on <see cref="CreditAccount"/>/<see cref="CreditCard"/>
/// as of this slice, even though <c>ICreditAccountLifecycleService.CanChangeCurrencyAsync</c>
/// (the guard the spec's §2.1 table calls for reusing) does exist. This screen therefore does NOT offer
/// currency editing in edit mode at all — the Currency picker stays disabled with a fixed explanatory
/// message, and <see cref="SelectedCurrency"/> is never written back to the card. Adding the missing
/// Domain mutator is out of this slice's scope (App/UI layer only per the coordinating task) and is
/// called out here so it isn't mistaken for an oversight.</para>
/// </summary>
[QueryProperty(nameof(CreditAccountIdText), "creditAccountId")]
public sealed partial class AddCreditCardViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;
    private readonly ICardNetworkRepository _networkRepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsAmountOwedVisible))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyPickerEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyLockedMessageVisible))]
    private Guid? editingCreditAccountId;

    /// <summary>Same defensive-parse idiom as <c>AddAccountViewModel.AccountIdText</c>.</summary>
    [ObservableProperty]
    private string? creditAccountIdText;

    partial void OnCreditAccountIdTextChanged(string? value) =>
        EditingCreditAccountId = Guid.TryParse(value, out var parsed) ? parsed : null;

    public bool IsEditMode => EditingCreditAccountId is not null;

    public string PageTitle => IsEditMode ? AppResources.AddCreditCard_EditTitle : AppResources.AddCreditCard_Title;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedInstitution;

    [ObservableProperty]
    private NamedOption? selectedNetwork;

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

    public bool IsAmountOwedVisible => !IsEditMode;

    /// <summary>
    /// Always disabled in edit mode -- see this class's doc comment's "Known gap" note: no Domain
    /// mutator exists to actually apply a currency change on a <see cref="CreditCard"/>, so this is not
    /// gated by <c>CanChangeCurrencyAsync</c> the way <c>AddAccountViewModel.IsCurrencyPickerEnabled</c>
    /// is -- there would be nothing safe to do with a "yes" answer here.
    /// </summary>
    public bool IsCurrencyPickerEnabled => !IsEditMode;

    public bool IsCurrencyLockedMessageVisible => IsEditMode;

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public ObservableCollection<NamedOption> InstitutionOptions { get; } = [];

    /// <summary>
    /// Drives an explanatory hint on the page (found via direct user feedback: "al momento de
    /// registrar una tarjeta de credito debe mostrar las entidades financieras registradas" -- an
    /// empty Picker with no institutions yet and no explanation reads as broken, since neither picker
    /// supports inline-add (see this class's own doc comment) and the only way out is a silent
    /// context-switch to Settings the screen never suggests on its own).
    /// </summary>
    public bool HasNoInstitutions => HasLoadedOptions && InstitutionOptions.Count == 0;

    [ObservableProperty]
    private bool hasLoadedOptions;

    /// <summary>
    /// Prepends a "None" sentinel (<see cref="Guid.Empty"/>, resolved via <see cref="AsNullableId"/>) —
    /// same idiom <c>AddTransactionViewModel.People</c> already uses for its optional Payer/Beneficiary
    /// pickers — since <see cref="Domain.CreditAccounts.CreditCard.NetworkId"/> is genuinely optional.
    /// </summary>
    public ObservableCollection<NamedOption> NetworkOptions { get; } = [];

    public AddCreditCardViewModel(
        ICreditAccountRepository creditAccountRepository,
        IFinancialInstitutionRepository institutionRepository,
        ICardNetworkRepository networkRepository)
    {
        _creditAccountRepository = creditAccountRepository;
        _institutionRepository = institutionRepository;
        _networkRepository = networkRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var institutions = await _institutionRepository.GetAllAsync();
        var networks = await _networkRepository.GetAllAsync();

        InstitutionOptions.Clear();
        foreach (var institution in institutions.OrderBy(i => i.Name))
            InstitutionOptions.Add(new NamedOption(institution.Id, institution.Name));

        NetworkOptions.Clear();
        NetworkOptions.Add(new NamedOption(Guid.Empty, AppResources.AddTransaction_NoneOption));
        foreach (var network in networks.OrderBy(n => n.Name))
            NetworkOptions.Add(new NamedOption(network.Id, network.Name));

        HasLoadedOptions = true;
        OnPropertyChanged(nameof(HasNoInstitutions));

        if (EditingCreditAccountId is not { } creditAccountId)
            return;

        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId);
        if (creditAccount is not CreditCard card)
        {
            ErrorMessage = AppResources.AddCreditCard_EditNotFound;
            return;
        }

        Name = card.Name;
        SelectedCurrency = card.Currency;
        SelectedInstitution = card.InstitutionId is { } institutionId
            ? InstitutionOptions.FirstOrDefault(o => o.Id == institutionId)
            : null;
        SelectedNetwork = card.NetworkId is { } networkId
            ? NetworkOptions.FirstOrDefault(o => o.Id == networkId)
            : NetworkOptions[0];
        LastFourDigits = card.LastFourDigits;
        CreditLimitText = card.CreditLimit.ToString("N2", CultureInfo.CurrentCulture);
        AnnualInterestRateText = card.AnnualInterestRate?.ToString("N2", CultureInfo.CurrentCulture);
        MonthlyInterestRateText = card.MonthlyInterestRate?.ToString("N2", CultureInfo.CurrentCulture);
        StatementCutOffDayText = card.StatementCutOffDay.ToString(CultureInfo.CurrentCulture);
        PaymentDueDayText = card.PaymentDueDay.ToString(CultureInfo.CurrentCulture);
        Notes = card.Notes;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!TryValidateCommonFields(out var creditLimit, out var annualInterestRate, out var monthlyInterestRate, out var statementCutOffDay, out var paymentDueDay))
            return;

        if (IsEditMode)
        {
            await SaveEditAsync(creditLimit, annualInterestRate, monthlyInterestRate, statementCutOffDay, paymentDueDay);
            return;
        }

        if (!decimal.TryParse(OpeningAmountOwedText, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingAmountOwed) || openingAmountOwed < 0)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationAmountOwedInvalid;
            return;
        }

        var creditCard = new CreditCard(
            Name,
            SelectedCurrency,
            SelectedInstitution!.Id,
            creditLimit,
            statementCutOffDay,
            paymentDueDay,
            openingAmountOwed,
            string.IsNullOrWhiteSpace(LastFourDigits) ? null : LastFourDigits,
            annualInterestRate,
            monthlyInterestRate,
            Notes,
            AsNullableId(SelectedNetwork));

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

    /// <summary>
    /// Validates every field shared between Add and Edit (everything except
    /// <see cref="OpeningAmountOwedText"/>, which only Add mode has). Sets <see cref="ErrorMessage"/>
    /// and returns <see langword="false"/> on the first failure, mirroring the existing inline-checks
    /// style this screen already used before edit mode existed.
    /// </summary>
    private bool TryValidateCommonFields(
        out decimal creditLimit,
        out decimal? annualInterestRate,
        out decimal? monthlyInterestRate,
        out int statementCutOffDay,
        out int paymentDueDay)
    {
        creditLimit = 0;
        annualInterestRate = null;
        monthlyInterestRate = null;
        statementCutOffDay = 0;
        paymentDueDay = 0;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationNameRequired;
            return false;
        }

        if (SelectedInstitution is null)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationIssuerRequired;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(LastFourDigits) &&
            (LastFourDigits.Length != 4 || !LastFourDigits.All(char.IsDigit)))
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationLastFourDigitsInvalid;
            return false;
        }

        if (!decimal.TryParse(CreditLimitText, NumberStyles.Number, CultureInfo.CurrentCulture, out creditLimit) || creditLimit <= 0)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationCreditLimitInvalid;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(AnnualInterestRateText))
        {
            if (!decimal.TryParse(AnnualInterestRateText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedAnnualRate) || parsedAnnualRate < 0)
            {
                ErrorMessage = AppResources.AddCreditCard_ValidationAnnualInterestRateInvalid;
                return false;
            }

            annualInterestRate = parsedAnnualRate;
        }

        if (!string.IsNullOrWhiteSpace(MonthlyInterestRateText))
        {
            if (!decimal.TryParse(MonthlyInterestRateText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedMonthlyRate) || parsedMonthlyRate < 0)
            {
                ErrorMessage = AppResources.AddCreditCard_ValidationMonthlyInterestRateInvalid;
                return false;
            }

            monthlyInterestRate = parsedMonthlyRate;
        }

        if (!int.TryParse(StatementCutOffDayText, NumberStyles.Integer, CultureInfo.CurrentCulture, out statementCutOffDay) || statementCutOffDay is < 1 or > 31)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationStatementCutOffDayInvalid;
            return false;
        }

        if (!int.TryParse(PaymentDueDayText, NumberStyles.Integer, CultureInfo.CurrentCulture, out paymentDueDay) || paymentDueDay is < 1 or > 31)
        {
            ErrorMessage = AppResources.AddCreditCard_ValidationPaymentDueDayInvalid;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Edit-mode half of <see cref="SaveAsync"/> (edit/delete slice spec §2.1) -- calls
    /// <see cref="CreditCard.UpdateDetails"/> instead of constructing a new card. <c>AmountOwed</c> is
    /// never touched here, and neither is <c>Currency</c> -- see this class's doc comment's "Known gap"
    /// note (no Domain mutator exists to change it).
    /// </summary>
    private async Task SaveEditAsync(
        decimal creditLimit, decimal? annualInterestRate, decimal? monthlyInterestRate, int statementCutOffDay, int paymentDueDay)
    {
        IsBusy = true;
        try
        {
            var creditAccount = await _creditAccountRepository.GetByIdAsync(EditingCreditAccountId!.Value);
            if (creditAccount is not CreditCard card)
            {
                ErrorMessage = AppResources.AddCreditCard_EditNotFound;
                return;
            }

            card.Rename(Name);
            card.UpdateNotes(Notes);

            card.UpdateDetails(
                SelectedInstitution!.Id,
                AsNullableId(SelectedNetwork),
                string.IsNullOrWhiteSpace(LastFourDigits) ? null : LastFourDigits,
                creditLimit,
                statementCutOffDay,
                paymentDueDay,
                annualInterestRate,
                monthlyInterestRate);

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

    /// <summary>Same "None" sentinel convention as <c>AddTransactionViewModel.AsNullableId</c>.</summary>
    private static Guid? AsNullableId(NamedOption? option) =>
        option is null || option.Id == Guid.Empty ? null : option.Id;
}
