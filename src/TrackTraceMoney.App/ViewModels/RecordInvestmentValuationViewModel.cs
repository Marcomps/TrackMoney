using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Create/edit a single <see cref="InvestmentValuation"/> (README §23). After saving, always recomputes
/// which recorded valuation now has the latest <see cref="InvestmentValuation.AsOfDate"/> (a fresh,
/// post-write query — never trusted from in-memory state) and pushes that valuation's value onto the
/// parent <see cref="InvestmentFund"/> via <see cref="InvestmentFund.RecordValuation"/>. This means a
/// backdated valuation never changes the fund's current balance, but editing/repositioning what WAS the
/// latest row correctly recomputes which one wins.
/// </summary>
[QueryProperty(nameof(InvestmentFundIdText), "investmentFundId")]
[QueryProperty(nameof(ValuationId), "valuationId")]
public sealed partial class RecordInvestmentValuationViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly IInvestmentValuationRepository _valuationRepository;

    private InvestmentValuation? _existingValuation;

    [ObservableProperty]
    private Guid investmentFundId;

    /// <summary>
    /// The actual <c>[QueryProperty]</c> target -- see
    /// <c>CreditCardDetailViewModel.CreditAccountIdText</c>'s doc comment: unlike <see cref="ValuationId"/>
    /// below, this one was NOT written defensively despite the exact same MAUI Shell limitation applying
    /// (a non-nullable <see cref="Guid"/> can't be a direct <c>[QueryProperty]</c> target), because nobody
    /// had connected "this ID is always present" with "Convert.ChangeType still can't produce a Guid".
    /// </summary>
    [ObservableProperty]
    private string? investmentFundIdText;

    partial void OnInvestmentFundIdTextChanged(string? value)
    {
        if (Guid.TryParse(value, out var parsed))
            InvestmentFundId = parsed;
    }

    /// <summary>
    /// Bound as a plain string, not <c>Guid?</c> — mirrors <c>RecordCreditCardStatementViewModel.StatementId</c>'s
    /// remarks: MAUI Shell query parameters always arrive as strings, parsed defensively via
    /// <see cref="TryGetValuationId"/>. Absent/unparseable means create mode; a valid Guid means edit mode.
    /// </summary>
    [ObservableProperty]
    private string? valuationId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool isEditMode;

    [ObservableProperty]
    private DateTime asOfDate = DateTime.Today;

    [ObservableProperty]
    private string valueText = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public string Title => IsEditMode
        ? AppResources.RecordInvestmentValuation_EditTitle
        : AppResources.RecordInvestmentValuation_Title;

    public RecordInvestmentValuationViewModel(
        IFinancialAccountRepository financialAccountRepository,
        IInvestmentValuationRepository valuationRepository)
    {
        _financialAccountRepository = financialAccountRepository;
        _valuationRepository = valuationRepository;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (TryGetValuationId(out var existingValuationId))
            {
                _existingValuation = await _valuationRepository.GetByIdAsync(existingValuationId);
                if (_existingValuation is null)
                    return;

                IsEditMode = true;
                AsOfDate = _existingValuation.AsOfDate.ToDateTime(TimeOnly.MinValue);
                ValueText = _existingValuation.Value.ToString(CultureInfo.CurrentCulture);
            }
            else
            {
                IsEditMode = false;
                AsOfDate = DateTime.Today;
                ValueText = string.Empty;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!decimal.TryParse(ValueText, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) || value < 0)
        {
            ErrorMessage = AppResources.RecordInvestmentValuation_ValidationValueInvalid;
            return;
        }

        var asOfDateOnly = DateOnly.FromDateTime(AsOfDate);

        IsBusy = true;
        try
        {
            if (IsEditMode && _existingValuation is not null)
            {
                _existingValuation.UpdateValuation(value, asOfDateOnly);
                await _valuationRepository.SaveChangesAsync();
            }
            else
            {
                var valuation = new InvestmentValuation(InvestmentFundId, asOfDateOnly, value);
                await _valuationRepository.AddAsync(valuation);
                await _valuationRepository.SaveChangesAsync();
            }

            // Re-fetch rather than trust the just-saved value in isolation: a backdated/edited valuation
            // might not be the most recent one anymore (or might newly become it) once compared against
            // every other recorded valuation for this fund.
            var valuations = await _valuationRepository.GetForFundAsync(InvestmentFundId);
            var latestValuation = valuations
                .OrderByDescending(v => v.AsOfDate)
                .ThenByDescending(v => v.Id)
                .FirstOrDefault();

            if (latestValuation is not null && await _financialAccountRepository.GetByIdAsync(InvestmentFundId) is InvestmentFund fund)
            {
                fund.RecordValuation(latestValuation.Value);
                await _financialAccountRepository.SaveChangesAsync();
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (DbUpdateException)
        {
            // The (InvestmentFundId, AsOfDate) unique index is the only constraint this save can hit --
            // reachable in practice, not just theoretically: AddInvestmentFundViewModel itself seeds an
            // opening-balance valuation dated at the fund's own InvestmentDate (defaults to today), so
            // "record a valuation" in create mode on a same-day-created fund, with the date left at its
            // own today-default, collides with that seed row. Previously unhandled -- crashed the app.
            ErrorMessage = AppResources.RecordInvestmentValuation_ValidationDuplicateDate;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync() =>
        await Shell.Current.GoToAsync("..");

    private bool TryGetValuationId(out Guid valuationIdValue) =>
        Guid.TryParse(ValuationId, out valuationIdValue);
}
