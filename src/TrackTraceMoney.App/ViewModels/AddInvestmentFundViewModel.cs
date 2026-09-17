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
/// Creates a new <see cref="InvestmentFund"/> (README §23). Also seeds the fund's valuation history with
/// one <see cref="InvestmentValuation"/> dated at <see cref="InvestmentDate"/> for the opening balance, so
/// the fund's detail screen has at least one history point immediately (mirrors how a term deposit or bank
/// account's opening balance is implicitly its first known value — here it has to be explicit because
/// valuations are their own tracked entity).
///
/// Institution is picked from the user's own growing <see cref="IFinancialInstitutionRepository"/> list —
/// see the financial-institution-card-network-slice-spec's Decision 4. Required (mirrors the old
/// free-text Institution field's required-ness). No inline-add — a user without their bank listed yet
/// leaves this screen, adds it via Settings, and comes back, same as Category/Person elsewhere in this app.
/// </summary>
public sealed partial class AddInvestmentFundViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly IInvestmentValuationRepository _valuationRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedInstitution;

    [ObservableProperty]
    private CurrencyCode selectedCurrency = CurrencyCode.USD;

    [ObservableProperty]
    private DateTime investmentDate = DateTime.Today;

    [ObservableProperty]
    private string contributionsText = string.Empty;

    [ObservableProperty]
    private string openingBalanceText = "0";

    [ObservableProperty]
    private string withdrawalsText = "0";

    [ObservableProperty]
    private string feesText = "0";

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public ObservableCollection<NamedOption> InstitutionOptions { get; } = [];

    public AddInvestmentFundViewModel(
        IFinancialAccountRepository financialAccountRepository,
        IInvestmentValuationRepository valuationRepository,
        IFinancialInstitutionRepository institutionRepository)
    {
        _financialAccountRepository = financialAccountRepository;
        _valuationRepository = valuationRepository;
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
            ErrorMessage = AppResources.AddInvestmentFund_ValidationNameRequired;
            return;
        }

        if (SelectedInstitution is null)
        {
            ErrorMessage = AppResources.AddInvestmentFund_ValidationInstitutionRequired;
            return;
        }

        if (!decimal.TryParse(ContributionsText, NumberStyles.Number, CultureInfo.CurrentCulture, out var contributions) || contributions <= 0)
        {
            ErrorMessage = AppResources.AddInvestmentFund_ValidationContributionsInvalid;
            return;
        }

        if (!decimal.TryParse(OpeningBalanceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingBalance) || openingBalance < 0)
        {
            ErrorMessage = AppResources.AddInvestmentFund_ValidationOpeningBalanceInvalid;
            return;
        }

        if (!decimal.TryParse(WithdrawalsText, NumberStyles.Number, CultureInfo.CurrentCulture, out var withdrawals) || withdrawals < 0)
        {
            ErrorMessage = AppResources.AddInvestmentFund_ValidationWithdrawalsInvalid;
            return;
        }

        if (!decimal.TryParse(FeesText, NumberStyles.Number, CultureInfo.CurrentCulture, out var fees) || fees < 0)
        {
            ErrorMessage = AppResources.AddInvestmentFund_ValidationFeesInvalid;
            return;
        }

        var investmentDateOnly = DateOnly.FromDateTime(InvestmentDate);

        var fund = new InvestmentFund(
            Name,
            SelectedCurrency,
            SelectedInstitution.Id,
            investmentDateOnly,
            contributions,
            openingBalance,
            withdrawals,
            fees,
            Notes);

        IsBusy = true;
        try
        {
            // Both AddAsync calls are tracked by the same shared DbContext (see RepositoryBase/
            // DbAccessGate remarks — this app's DI resolves TrackTraceMoneyDbContext as a single
            // long-lived instance), so one SaveChangesAsync call commits both inserts atomically.
            await _financialAccountRepository.AddAsync(fund);
            await _valuationRepository.AddAsync(new InvestmentValuation(fund.Id, investmentDateOnly, openingBalance));
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
