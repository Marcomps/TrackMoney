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
/// </summary>
public sealed partial class AddCreditCardViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;
    private readonly ICardNetworkRepository _networkRepository;

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

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public ObservableCollection<NamedOption> InstitutionOptions { get; } = [];

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

        if (SelectedInstitution is null)
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
            SelectedInstitution.Id,
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

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Same "None" sentinel convention as <c>AddTransactionViewModel.AsNullableId</c>.</summary>
    private static Guid? AsNullableId(NamedOption? option) =>
        option is null || option.Id == Guid.Empty ? null : option.Id;
}
