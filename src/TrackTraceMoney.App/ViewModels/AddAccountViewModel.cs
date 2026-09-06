using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddAccountViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;

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
    private string? bankName;

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

    public IReadOnlyList<AccountKind> AvailableKinds { get; } = Enum.GetValues<AccountKind>();

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public bool IsBankKind => SelectedKind == AccountKind.Bank;

    public bool IsSavingsKind => SelectedKind == AccountKind.Savings;

    public AddAccountViewModel(IFinancialAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
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

        if (!decimal.TryParse(OpeningBalanceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingBalance))
        {
            ErrorMessage = AppResources.AddAccount_ValidationBalanceInvalid;
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

        FinancialAccount account = SelectedKind switch
        {
            AccountKind.Cash => new CashAccount(Name, SelectedCurrency, openingBalance, Notes),
            AccountKind.Bank => new BankAccount(Name, SelectedCurrency, openingBalance, BankName, AccountNumberLast4, Notes),
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

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
