using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Minimal "raise" screen (recurring-income slice spec §4, Decision F): shows the current
/// <see cref="Name"/> read-only for context, lets the user change <see cref="AmountText"/>, and
/// calls <c>RecurringIncome.UpdateAmount</c> on save. Not a reuse of
/// <see cref="AddRecurringIncomeViewModel"/>'s insert flow -- that would require turning
/// <c>SaveAsync</c> into an insert-or-update flow keyed on an optional Id, more invasive than the
/// ask of changing one field.
/// </summary>
[QueryProperty(nameof(RecurringIncomeIdText), "id")]
public sealed partial class EditRecurringIncomeAmountViewModel : ObservableObject
{
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;

    [ObservableProperty]
    private Guid recurringIncomeId;

    /// <summary>
    /// The actual <c>[QueryProperty]</c> target. MAUI Shell's query-string navigation always hands a
    /// <c>string</c> to the receiving property and internally does a plain <c>Convert.ChangeType</c> --
    /// which throws <see cref="InvalidCastException"/> for a non-nullable <see cref="Guid"/> target
    /// (<see cref="Guid"/> has no <see cref="IConvertible"/> implementation), crashing the app on every
    /// single navigation into this page. Bound here as a string and parsed defensively instead, same
    /// idiom this codebase already uses elsewhere (e.g. <c>CreditCardDetailViewModel.CreditAccountIdText</c>).
    /// </summary>
    [ObservableProperty]
    private string? recurringIncomeIdText;

    partial void OnRecurringIncomeIdTextChanged(string? value)
    {
        if (Guid.TryParse(value, out var parsed))
            RecurringIncomeId = parsed;
    }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public EditRecurringIncomeAmountViewModel(IRecurringIncomeRepository recurringIncomeRepository)
    {
        _recurringIncomeRepository = recurringIncomeRepository;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (RecurringIncomeId == Guid.Empty)
            return;

        var recurringIncome = await _recurringIncomeRepository.GetByIdAsync(RecurringIncomeId);
        if (recurringIncome is null)
            return;

        Name = recurringIncome.Name;
        AmountText = recurringIncome.Amount.ToString("N2", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            ErrorMessage = AppResources.EditRecurringIncomeAmount_ValidationAmountInvalid;
            return;
        }

        IsBusy = true;
        try
        {
            var recurringIncome = await _recurringIncomeRepository.GetByIdAsync(RecurringIncomeId);
            if (recurringIncome is null)
            {
                ErrorMessage = AppResources.EditRecurringIncomeAmount_ValidationAmountInvalid;
                return;
            }

            recurringIncome.UpdateAmount(amount);
            await _recurringIncomeRepository.SaveChangesAsync();
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
